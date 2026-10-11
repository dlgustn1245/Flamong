using UnityEngine;
using UnityEngine.UI;

public class EggSelectScene : MonoBehaviour
{
    public Button[] eggSlots;
    
    [Header("Confirm Panel")]
    public GameObject confirmPanel;
    public Image eggImage;
    public Button yes, no;

    void Start()
    {
        BindButtonEvents();
    }

    void OnClickEgg(int idx)
    {
        print($"Select Egg index: {idx}");
        confirmPanel.SetActive(true);
        eggImage.sprite = eggSlots[idx].image.sprite;
        SessionState.Instance.eggIdx = idx;
    }

    void BindButtonEvents()
    {
        for (int i = 0; i < eggSlots.Length; i++)
        {
            int idx = i;
            eggSlots[idx].onClick.AddListener(() =>
            {
                OnClickEgg(idx);
            });
        }
        
        yes.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.HatchScene);
        });
        
        no.onClick.AddListener(() =>
        {
            confirmPanel.SetActive(false);
        });
    }

    void OnDestroy()
    {
        foreach (var eggSlot in eggSlots)
        {
            eggSlot.onClick.RemoveAllListeners();
        }
        yes.onClick.RemoveAllListeners();
        no.onClick.RemoveAllListeners();
    }
}
