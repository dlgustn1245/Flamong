using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;   // using System.Diagnostics 의 Debug 와 겹친다

// 빌드 버튼을 누르면 버전을 묻고, 입력한 버전(Player Settings Version)으로 빌드한다. Title 이 이 값을 보여 준다.
// 웹 빌드가 성공하면 운영에 올릴지 묻고, 고르면 Server/deploy 의 배치 파일을 돌려 끝까지 올린다
public class BuildVersionPrompt : EditorWindow
{
    string version;
    bool confirmed;

    [InitializeOnLoadMethod]
    static void Register()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuild);
    }

    static void OnBuild(BuildPlayerOptions options)
    {
        var window = CreateInstance<BuildVersionPrompt>();
        window.titleContent = new GUIContent("빌드 버전");
        window.version = PlayerSettings.bundleVersion;
        window.minSize = window.maxSize = new Vector2(320, 110);
        window.ShowModal();   // 창을 닫을 때까지 여기서 기다린다
        if (!window.confirmed)
        {
            return;
        }
        PlayerSettings.bundleVersion = window.version;
        AssetDatabase.SaveAssets();
        var startedAt = DateTime.UtcNow;
        // 빌드가 끝나면 Unity 가 빌드 폴더를 탐색기로 여는데, 그 창이 에디터와 배포 창을 가린다. 열지 않게 한다
        options.options &= ~BuildOptions.ShowBuiltPlayer;
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        AskDeploy(options, startedAt);
    }

    // 방금 끝난 웹 빌드가 성공했을 때만 묻는다. 고른 뒤로는 더 묻지 않는다(배치 파일의 /y).
    // 서버 + 웹은 서버를 먼저 올리고(테스트 포함), 실패하면 웹은 올리지 않는다
    static void AskDeploy(BuildPlayerOptions options, DateTime startedAt)
    {
        var report = BuildReport.GetLatestReport();
        if (options.target != BuildTarget.WebGL || report == null
            || report.summary.result != BuildResult.Succeeded || report.summary.buildEndedAt < startedAt)
        {
            return;
        }
        // 창을 그냥 닫으면 「안 함」(Cancel)이다
        var choice = EditorDialog.DisplayComplexDecisionDialog("배포",
            "빌드가 끝났습니다. 운영(flamong.lhsdev.info)에 올릴까요?\n고르면 더 묻지 않고 끝까지 올립니다.",
            defaultButtonText: "웹만", altButtonText: "서버 + 웹", cancelButtonText: "안 함", iconType: DialogIconType.Info);
        if (choice == DialogResult.Cancel)
        {
            return;
        }

        // 빌드 폴더의 deploy.bat 은 늘 원본(Server/deploy/deploy-web.bat)으로 덮어쓴다. 사본이 원본과 어긋나지 않게
        string web = Path.Combine(options.locationPathName, "deploy.bat");
        File.Copy(Path.GetFullPath("Server/deploy/deploy-web.bat"), web, true);

        var log = new StringBuilder();
        bool ok = true;
        if (choice == DialogResult.AlternateAction)
        {
            ok = RunBat(Path.GetFullPath("Server/deploy/deploy-server.bat"), "서버를 올리는 중...", log);
        }
        if (ok)
        {
            ok = RunBat(web, "웹 빌드를 올리는 중...", log);
        }
        EditorUtility.ClearProgressBar();

        string text = log.ToString().Trim();
        if (ok)
        {
            Debug.Log("배포 끝\n" + text);
        }
        else
        {
            Debug.LogError("배포 실패\n" + text);
        }
        // 창에는 끝부분만. 전체는 콘솔에 있다
        string tail = text.Length > 900 ? "...\n" + text.Substring(text.Length - 900) : text;
        // 기본 대화 상자는 느낌표 아이콘이라 성공도 실패처럼 보인다. 성공은 정보, 실패는 오류 아이콘으로
        EditorDialog.DisplayAlertDialog(ok ? "배포 끝" : "배포 실패", tail, "확인", ok ? DialogIconType.Info : DialogIconType.Error);
    }

    // 배치 파일을 창 없이 /y 로 돌리고 끝날 때까지 기다린다. 출력은 log 에 모으고, 종료 코드 0 이면 true.
    // 5분이 지나도 안 끝나면(네트워크 멈춤) 자식 프로세스까지 끄고 실패로 본다. 그동안 에디터는 멈춰 있다
    public static bool RunBat(string bat, string title, StringBuilder log)
    {
        EditorUtility.DisplayProgressBar("배포", title, 0.5f);
        var info = new ProcessStartInfo("cmd.exe", $"/c \"\"{bat}\" /y 2>&1\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        using (var process = new Process { StartInfo = info })
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    lock (log)
                    {
                        log.AppendLine(e.Data);
                    }
                }
            };
            process.Start();
            process.StandardInput.Close();   // 혹시 입력을 기다리는 곳이 있어도 바로 지나가게
            process.BeginOutputReadLine();
            if (!process.WaitForExit(300000))
            {
                Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {process.Id}") { UseShellExecute = false, CreateNoWindow = true })?.WaitForExit();
                lock (log)
                {
                    log.AppendLine("[실패] 5분이 지나도 끝나지 않아 멈췄습니다.");
                }
                return false;
            }
            process.WaitForExit();   // 남은 출력을 다 받는다
            return process.ExitCode == 0;
        }
    }

    void OnGUI()
    {
        version = EditorGUILayout.TextField("버전", version);
        bool valid = System.Version.TryParse(version, out var v) && v.Build >= 0 && v.Revision < 0;
        if (GUILayout.Button("다음 버전 추천") && valid)
        {
            version = $"{v.Major}.{v.Minor}.{v.Build + 1}";
            GUI.FocusControl(null);
        }
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = valid;
        if (GUILayout.Button("빌드"))
        {
            confirmed = true;
            Close();
        }
        GUI.enabled = true;
        if (GUILayout.Button("취소"))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}