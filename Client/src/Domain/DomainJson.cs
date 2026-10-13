using System.Text.Json.Serialization;

namespace LibreKO.Domain;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(FortuneDeck))]
[JsonSerializable(typeof(DisguiseForm[]))]
[JsonSerializable(typeof(AuctionScheduleLot[]))]
[JsonSerializable(typeof(CombineBook))]
internal partial class DomainJson : JsonSerializerContext;
