using System;

namespace ServerData
{
	[Serializable]
	public class CheckNickNameResponseBody
	{
		public bool available;
	}

	public class PostPlayerRequestBody
	{
		public string team;
		public string name;
		public string nickname;
		public string password;
	}

	// POST /players/login. 응답은 가입과 같은 { playerId } 라 PostPlayerResponseBody 를 쓴다
	public class LoginRequestBody
	{
		public string nickname;
		public string password;
	}

	[Serializable]
	public class PostPlayerResponseBody
	{
		public string playerId;
	}
	
	public class PatchPlayerRequestBody
	{
		public string team;
		public string name;
	}

	[Serializable]
	public class OkBody
	{
		public bool ok;
	}

	public class PostCharacterRequestBody
	{
		public int eggId;
	}

	[Serializable]
	public class PostCharacterResponseBody
	{
		public int speciesId;
	}
	
	// 두 응답이 같이 쓰는 성장 상태. 화면 갱신은 이것 하나로 한다
	[Serializable]
	public class Growth
	{
		public int hearts;          // 지금 쓸 수 있는 하트 (0~15)
		public int heartsMax;       // 15
		public int affection;       // 누적 애정도 (0~175). 참고용
		public int level;           // 1~5
		public int levelProgress;   // 이 레벨이 된 뒤 모은 애정도
		public int levelNeed;       // 이 레벨을 다 채우는 데 필요한 양
		public bool completed;
	}

	public class EmotionRequestBody
	{
		public string emotion;
	}

	// POST /tap · POST /emotions
	[Serializable]
	public class TapBody
	{
		public bool levelUp;
		public Growth growth;
	}

	// GET /me
	[Serializable]
	public class MeBody
	{
		public string team;
		public string name;
		public string nickname;
		public int speciesId;
		public bool emotionToday;
		public bool potionReady;
		public bool newsReady;   // 오늘의 소식 담당이고 아직 안 씀 (확성기 빨간 점)
		public Growth growth;
	}
	
	[Serializable]
	public class CollectionBody
	{
		public int[] levels;       // [speciesId] = 도달한 최고 레벨 (0 = 아직 없음). 지금 캐릭터 · 완성한 캐릭터만 센다
		public int[] completed;
		public int speciesId;
	}
	
	// GET /missions
	[Serializable]
	public class MissionsBody
	{
		public bool canDrink;
		public VisitMission visit;
		public HiddenMission[] hidden;
	}

	[Serializable]
	public class VisitMission
	{
		public int progress;
		public int need;
	}

	[Serializable]
	public class HiddenMission
	{
		public string id;
		public bool done;
		public bool claimed;
	}

	// POST /potions/claim. 응답은 TapBody 를 그대로 쓴다
	[Serializable]
	public class ClaimRequestBody
	{
		public string missionId;
	}

	// GET /posts?kind=garden|news
	[Serializable]
	public class PostsBody
	{
		public bool canWrite;
		public PostItem[] posts;   // 최신순 30개
	}

	[Serializable]
	public class PostItem
	{
		public string body;
		public string date;       // KST YYYY-MM-DD
		public string nickname;   // 소식만. 꽃밭은 ""
	}

	// POST /posts. 응답은 OkBody
	public class PostRequestBody
	{
		public string kind;   // "garden" | "news"
		public string body;
	}
	
	// POST /games/start
	public class GameStartRequestBody
	{
		public string game;   // GameList.Tidy
	}

	[Serializable]
	public class GameStartBody
	{
		public GameRules rules;
	}

	// 한 판의 수치. 서버 rules/game.js 가 정해 내려 준다(클라이언트에 적어 두지 않는다)
	[Serializable]
	public class GameRules
	{
		public int durationSec;    // 판 길이 (초)
		public int countdownSec;   // 카운트다운 (초)
		public int minTapMs;       // 받아들이는 입력 사이 최소 간격
		public int lockMs;         // 틀린 뒤 입력 잠금
		public int changeEvery;    // 맞힌 개수가 이 수의 배수가 될 때마다 서랍 한쪽이 바뀐다
		public int maxRun;         // 같은 쪽 연속 최대
		public int[] comboAt;      // 맞히기 전 콤보가 이 값 이상이면 1점씩 더한다
	}

	// POST /scores
	public class ScoreRequestBody
	{
		public int score;
	}

	[Serializable]
	public class ScoreBody
	{
		public int best;        // 이번 달 이 게임의 내 최고
		public int rank;        // 동점은 같은 등수
		public int gap;         // 다음 등수까지 점수. 1등이면 0
		public int gain;        // 이번에 오른 애정도. 하루 첫 판이 아니면 0
		public bool levelUp;
		public Growth growth;
	}

	// GET /rankings?month=YYYY-MM
	[Serializable]
	public class Board
	{
		public string month;    // YYYY-MM
		public string game;     // 출시 전 달 · 다음 달 이후는 ""
		public RankRow[] top;   // 상위 20
		public int rank;        // 내 등수. 기록이 없으면 0
		public int best;        // 내 최고
		public int gap;         // 다음 등수까지 점수
	}

	[Serializable]
	public class RankRow
	{
		public int rank;
		public string nickname;
		public int best;
	}

	// GET /games/months
	[Serializable]
	public class MonthsBody
	{
		public string current;       // 이번 달 YYYY-MM
		public MonthItem[] months;   // 올해 1~12월
	}

	[Serializable]
	public class MonthItem
	{
		public string month;   // YYYY-MM
		public string game;    // 출시 전 달 · 다음 달 이후는 ""
		public int best;
		public int rank;       // 기록이 없으면 0. 0점 판도 기록이라 best 로 판단하지 않는다
	}

	// 본문 없는 POST 용. JsonUtility 로 "{}" 가 된다 (빈 본문이면 서버가 400)
	public class EmptyBody { }
}
