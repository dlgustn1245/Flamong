using System;

namespace ServerData
{
	public class CheckNickNameResponse
	{
		public int statusCode;
		public CheckNickNameResponseBody body;
	}

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

	// POST /players/login. 응답은 가입과 같은 { playerId } 라 PostPlayerResponse 를 쓴다
	public class LoginRequestBody
	{
		public string nickname;
		public string password;
	}

	public class PostPlayerResponse
	{
		public int statusCode;
		public PostPlayerResponseBody body;
	}

	[Serializable]
	public class PostPlayerResponseBody
	{
		public string playerId;
	}

	public class PostCharacterRequestBody
	{
		public int eggId;
	}

	public class PostCharacterResponse
	{
		public int statusCode;
		public PostCharacterResponseBody body;
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

	// POST /tap
	public class TapResponse { public int statusCode; public TapBody body; }

	[Serializable]
	public class TapBody
	{
		public bool levelUp;
		public Growth growth;
	}

	// GET /me
	public class MeResponse
	{
		public int statusCode; 
		public MeBody body;
	}

	[Serializable]
	public class MeBody
	{
		public string team;
		public string name;
		public string nickname;
		public int speciesId;
		public bool emotionToday;
		public Growth growth;
	}
	
	// 본문 없는 POST 용. JsonUtility 로 "{}" 가 된다 (빈 본문이면 서버가 400)
	public class EmptyBody { }
}
