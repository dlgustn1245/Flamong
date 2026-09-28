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
}
