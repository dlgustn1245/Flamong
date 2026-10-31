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
	
	// 본문 없는 POST 용. JsonUtility 로 "{}" 가 된다 (빈 본문이면 서버가 400)
	public class EmptyBody { }
}
