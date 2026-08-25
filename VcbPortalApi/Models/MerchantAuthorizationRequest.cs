using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace VcbPortalApi.Models
{
    public sealed class MerchantAuthorizationRequest
    {
        [Required]
        [JsonProperty("bid")]
        public string Bid { get; set; } = string.Empty;

        [Required]
        [JsonProperty("mid")]
        public string Mid { get; set; } = string.Empty;

        [Required]
        [JsonProperty("tid")]
        public string Tid { get; set; } = string.Empty;

        [Required]
        [JsonProperty("auditData")]
        public MerchantAuthorizationAuditData AuditData { get; set; } = new();

        [Required]
        [JsonProperty("requestID")]
        public string RequestId { get; set; } = string.Empty;
    }

    public sealed class MerchantAuthorizationAuditData
    {
        [JsonProperty("channel")]
        public string? Channel { get; set; }

        [JsonProperty("channelIp")]
        public string? ChannelIp { get; set; }

        [JsonProperty("channelUser")]
        public string? ChannelUser { get; set; }

        [JsonProperty("channelUserBranch")]
        public string? ChannelUserBranch { get; set; }

        [JsonProperty("channelTime")]
        public string? ChannelTime { get; set; }
    }
}