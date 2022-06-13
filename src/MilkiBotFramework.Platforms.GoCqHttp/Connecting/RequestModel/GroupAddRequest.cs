#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel
{
    public class GroupAddRequest
    {
        [JsonPropertyName("flag")]
        public string Flag { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("sub_type")]
        public string SubType { get; set; }

        [JsonPropertyName("approve")]
        public bool Approve { get; set; } = true;

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        public static GroupAddRequest GetInviteRefuse(string flag, string reason = null)
        {
            return new GroupAddRequest { Approve = false, Flag = flag, SubType = "invite", Type = "invite", Reason = reason };
        }

        public static GroupAddRequest GetInviteApprove(string flag)
        {
            return new GroupAddRequest { Approve = true, Flag = flag, SubType = "invite", Type = "invite" };
        }

        public static GroupAddRequest GetRefuseAsAdmin(string flag, string reason = null)
        {
            return new GroupAddRequest { Approve = false, Flag = flag, SubType = "add", Type = "add", Reason = reason };
        }

        public static GroupAddRequest GetApproveAsAdmin(string flag)
        {
            return new GroupAddRequest { Approve = true, Flag = flag, SubType = "add", Type = "add" };
        }
    }
}