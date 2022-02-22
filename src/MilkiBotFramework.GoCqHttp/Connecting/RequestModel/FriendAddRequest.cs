using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Connecting.RequestModel
{
    public class FriendAddRequest
    {
        [JsonPropertyName("flag")]
        public string Flag { get; set; }

        [JsonPropertyName("approve")]
        public bool Approve { get; set; } = true;

        [JsonPropertyName("remark")]
        public string Remark { get; set; }

        public static FriendAddRequest GetRefuse(string flag)
        {
            return new FriendAddRequest { Approve = false, Flag = flag };
        }
        public static FriendAddRequest GetApprove(string flag, string remark = null)
        {
            return new FriendAddRequest { Approve = true, Flag = flag, Remark = remark };
        }
    }
}
