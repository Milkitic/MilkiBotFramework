#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel
{
    public class GroupMsgResponse
    {
        [JsonPropertyName("reply")]
        public string Reply { get; set; }
    
        [JsonPropertyName("auto_escape")]
        public bool AutoEscape { get; set; }
    
        [JsonPropertyName("at_sender")]
        public bool AtSender { get; set; }
     
        [JsonPropertyName("delete")]
        public bool Delete { get; set; }
    
        [JsonPropertyName("kick")]
        public bool Kick { get; set; }
   
        [JsonPropertyName("ban")]
        public bool Ban { get; set; }
    }
}
