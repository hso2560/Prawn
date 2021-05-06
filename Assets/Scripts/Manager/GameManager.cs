using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.IO;
using System;

public enum Color_State
{
    BLACK,
    WHITE,
    RED,
    BLUE,
    YELLOW,
    GREEN,
    PURPLE,
    MAGENTA,
    CYAN,
    CLEAR,
    GRAY,
    ORANGE
}

public class GameManager : MonoSingleton<GameManager>
{
    [SerializeField] private SaveData saveData;  
    public SaveData savedData { get { return saveData; } }
    public ShopManager shopManager;
    public Dictionary<int, Prawn> idToPrawn;
    public Dictionary<Color_State, Color> eColor;

    private Animator prawnAnimator;

    private string filePath;
    private readonly string SaveFileName = "Savefile";
    private string savedJson;
    private Coroutine AutoTchCo;  //자동으로 새우가 돈 벌게 하는 코루틴
    public GameObject BlackPanel;  //로딩 처리를 위한 검은색 바탕

    public MyPrawn myPrawn;
    public Sprite[] prawnSprs;
    public Text coinTxt;

    [Header("메인씬에서 쓰이는 껐다켰다하는 UI들")]
    public GameObject[] mainObjs;

    [Header("건들면 안됨")]
    public List<GameObject> uiObjs;   //뒤로가기 버튼으로 없앨 수 있는 UI들

    public Image hpImage, mentalImage;
    public Text hpTxt, mentalTxt, systemTxt;

    #region 물고기 움직임 관련 변수
    //랜덤 위치 (maxPosition minPosition) 제한
    //랜덤 초 기다림 (max time min time) 제한
    private GameObject fishPooling;

    [Header("물고기 스폰 관련 변수")]
    [Tooltip("물고기 스폰 위치 최소 제한")]
    public Vector2 fishMinPosition;
    [Tooltip("물고기 스폰 위치 최대 제한")]
    public Vector2 fishMaxPosition;
    [Tooltip("물고기 스폰 최소 시간")]
    [SerializeField] private float fishMinTime;
    [Tooltip("물고기 스폰 최대 시간")]
    [SerializeField] private float fishMaxTime;

    #endregion

    private void Awake()
    {
        Application.runInBackground = true;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(1440, 2960, true);

        saveData = new SaveData();
        filePath = string.Concat(Application.persistentDataPath, "/", SaveFileName);

        Load();

        if (saveData.isFirstStart)
        {
            saveData.prawns.Add(new Prawn(false, 10, 300,1 ,100, 100, 50, 0, 10, 10, 0,2000, 300, "흰다리새우","(기본 새우 설명)" ,prawnSprs[0]));
            saveData.currentPrawn = saveData.prawns[0];
            DataLoad();
            saveData.isFirstStart = false;
        }

        prawnAnimator = myPrawn.GetComponent<Animator>();
        fishPooling = GameObject.Find("FishPooling");

        #region (혹시 잘못되면 바로 알아볼 수 있도록 코드 추가함) fishPooling 관련 예외 처리 (유니티 안에서만 실행됨)
#if UNITY_EDITOR
        if (fishPooling == null) //fishPooling이 없다면 실행
        {
            UnityEditor.EditorUtility.DisplayDialog("FishPooling 오류", "FishPooling이 없습니다. FishPooling을 추가해 주세요", "확인");
            UnityEditor.EditorApplication.isPlaying = false;
        }
        else if(fishPooling.transform.childCount <= 0)//fishPooling에 fish가 0개 이하라면 실행
        {
            UnityEditor.EditorUtility.DisplayDialog("FishPooling 오류", "FishPooling안에 Fish가 없습니다. Fish를 추가해 주세요", "확인");
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif
        #endregion

        StartCoroutine(FadeEffect(BlackPanel.GetComponent<Image>()));
        StartCoroutine(SpawnFish());
    }

    #region 저장/로드
    public void Save()  //저장
    {
        SaveData();
        savedJson = JsonUtility.ToJson(saveData);
        byte[] bytes = Encoding.UTF8.GetBytes(savedJson);
        string code = Convert.ToBase64String(bytes);
        File.WriteAllText(filePath, code);
    }

    public void Load()  //불러오기
    {
        if(File.Exists(filePath))
        {
            string code = File.ReadAllText(filePath);
            byte[] bytes = Convert.FromBase64String(code);
            savedJson = Encoding.UTF8.GetString(bytes);
            saveData = JsonUtility.FromJson<SaveData>(savedJson);
            DataLoad();
        }
    }
    private void DataLoad()  
    { 
        myPrawn.PrawnLoad(saveData.currentPrawn);
        SetData();
        InitDict();
        AutoTchCo =StartCoroutine(AutoTouch());
    }    
    public void SaveData()  
    {
        foreach(Prawn p in saveData.prawns)
        {
            if(p.id==saveData.currentPrawn.id)
            {
                saveData.prawns[saveData.prawns.IndexOf(p)] = saveData.currentPrawn;
                return;
            }
        }
    }
    public void SetData()  
    {
        coinTxt.text = saveData.coin.ToString();
        hpImage.fillAmount = (float)saveData.currentPrawn.hp / (float)saveData.currentPrawn.maxHp;
        hpTxt.text = string.Format("HP: {0}/{1}", saveData.currentPrawn.hp, saveData.currentPrawn.maxHp);
        mentalImage.fillAmount = (float)saveData.currentPrawn.curMental / (float)saveData.currentPrawn.mental;
        mentalTxt.text = string.Format("MENTAL: {0}/{1}", saveData.currentPrawn.curMental, saveData.currentPrawn.mental);
    }
    private void InitDict()
    {
        idToPrawn = new Dictionary<int, Prawn>();

        for(int i=0; i<saveData.prawns.Count; i++)
            idToPrawn.Add(saveData.prawns[i].id, saveData.prawns[i]);

        eColor = new Dictionary<Color_State, Color>();

        eColor.Add(Color_State.BLACK, Color.black);
        eColor.Add(Color_State.BLUE, Color.blue);
        eColor.Add(Color_State.GREEN, Color.green);
        eColor.Add(Color_State.PURPLE, new Color(193,0,255,255));
        eColor.Add(Color_State.RED, Color.red);
        eColor.Add(Color_State.WHITE, Color.white);
        eColor.Add(Color_State.YELLOW, Color.yellow);
        eColor.Add(Color_State.MAGENTA, Color.magenta);
        eColor.Add(Color_State.CYAN, Color.cyan);
        eColor.Add(Color_State.CLEAR, Color.clear);
        eColor.Add(Color_State.GRAY, Color.gray);
        eColor.Add(Color_State.ORANGE, new Color(255, 99, 0, 255));
    }

    #endregion

    public void Touch()  //화면 터치
    {
        if (saveData.currentPrawn.hp < saveData.currentPrawn.needHp || saveData.currentPrawn.curMental <= 0)
        {
            ActiveSystemPanel("체력 혹은 정신력이 부족합니다.");
            return;
        }

        saveData.coin += saveData.currentPrawn.power;
        coinTxt.text = saveData.coin.ToString();
        saveData.currentPrawn.touchCount++;
        if(saveData.currentPrawn.touchCount%5==0)
        {
            saveData.currentPrawn.curMental--;
            mentalImage.fillAmount = (float)saveData.currentPrawn.curMental / (float)saveData.currentPrawn.mental;
            mentalTxt.text = string.Format("MENTAL: {0}/{1}", saveData.currentPrawn.curMental, saveData.currentPrawn.mental);
        }

        saveData.currentPrawn.hp -= saveData.currentPrawn.needHp;
        hpImage.fillAmount = (float)saveData.currentPrawn.hp / (float)saveData.currentPrawn.maxHp;
        hpTxt.text = string.Format("HP: {0}/{1}", saveData.currentPrawn.hp, saveData.currentPrawn.maxHp);
        prawnAnimator.Play("PrawnAnimation");
    }

    private IEnumerator AutoTouch()  //자동 터치 코루틴
    {
        while(saveData.currentPrawn.isAutoWork)
        {
            yield return new WaitForSeconds(1);
            saveData.coin += saveData.currentPrawn.autoPowor;
            coinTxt.text = saveData.coin.ToString();
        }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(uiObjs.Count>0)
            {
                uiObjs[uiObjs.Count - 1].SetActive(false);
                uiObjs.RemoveAt(uiObjs.Count - 1);
            }
            else
            {
                //게임종료 패널 띄우기
            }
        }
    }

    private void OnApplicationQuit()
    {
        Save();
    }
    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            Save();
        }
    }

    public IEnumerator FadeEffect(Image img, float fadeTime=1f)  //이미지 fadein/out
    {
        float t = fadeTime / 100;
        Color c = img.color;

        if(img.gameObject.activeSelf)
        {
            while(c.a>0)
            {
                c.a -= 0.01f;
                img.color = c;
                yield return new WaitForSeconds(t);
            }
            img.gameObject.SetActive(false);
        }
        else
        {
            img.gameObject.SetActive(true);
            c.a = 0;
            while(c.a!=1)
            {
                c.a += 0.01f;
                img.color = c;
                yield return new WaitForSeconds(t);
            }
        }
    }

    public void ButtonUIClick(int n)  //메인씬의 UI On/Off시 사용할 함수
    {
        if (mainObjs[n].activeSelf) uiObjs.Remove(mainObjs[n]);
        else uiObjs.Add(mainObjs[n]);

        mainObjs[n].SetActive(!mainObjs[n].activeSelf);
        shopManager.UpgradeRenewal();
    }

    public void ActiveSystemPanel(string msg, Color_State _color=Color_State.BLACK, int font_size=95)  //시스템 메세지 띄우는 패널(함수)
    {
        mainObjs[1].SetActive(true);
        systemTxt.text = msg;
        systemTxt.color = eColor[_color];
        systemTxt.fontSize = font_size;
        uiObjs.Add(mainObjs[1]);
    }

    private IEnumerator SpawnFish()
    {
        yield return new WaitForSeconds(10f);

        Transform fishTransform = null;
        GameObject fishGO = null;

        Vector2 randomPosition = Vector2.zero;

        float randomWaitSecond = 0f;

        while (true)
        {
            if (fishPooling.transform.childCount > 0)
            {
                fishTransform = fishPooling.transform.GetChild(0);
                fishGO = fishTransform.gameObject;
            }
            else
            {
                yield return new WaitForSeconds(fishMinTime);
                continue;
            }

            fishGO.SetActive(true);
            randomPosition.x = fishMinPosition.x;
            randomPosition.y = UnityEngine.Random.Range(fishMinPosition.y, fishMaxPosition.y);
            fishTransform.position = randomPosition;
            fishTransform.SetParent(null, true);
            randomWaitSecond = UnityEngine.Random.Range(fishMinTime, fishMaxTime);
            yield return new WaitForSeconds(randomWaitSecond);
        }
    }

    public bool IsPrawnPossession(int compareId)  //어떤 새우가 현재 자신에게 보유중인지 체크할 함수
    {
        foreach(Prawn p in saveData.prawns)
        {
            if (p.id == compareId)
                return true;
        }
        return false;
    }
}
