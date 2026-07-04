using System.Text.Json.Serialization;

namespace ApiPlatform.Contracts;

/// <summary>
/// System.Text.Json source-generation context for the canonical contract types. The generator
/// emits serialization metadata at compile time, so these types serialize with no runtime
/// reflection — faster, trim/AOT-safe, and the serializable surface is explicit. Wire it into a
/// host via <c>options.TypeInfoResolverChain.Insert(0, ApiPlatformJsonContext.Default)</c>;
/// types not listed here fall through to the reflection resolver (so anonymous payloads still work).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Account))]
[JsonSerializable(typeof(AccountList))]
[JsonSerializable(typeof(Transaction))]
[JsonSerializable(typeof(TransactionList))]
[JsonSerializable(typeof(Customer))]
[JsonSerializable(typeof(CustomerList))]
[JsonSerializable(typeof(WorkItem))]
[JsonSerializable(typeof(WorkItemList))]
[JsonSerializable(typeof(Insight))]
[JsonSerializable(typeof(InsightList))]
public partial class ApiPlatformJsonContext : JsonSerializerContext
{
}
