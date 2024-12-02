using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DocumentModel;
using Newtonsoft.Json;
using Amazon.Runtime;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    public async Task<LambdaResponse> FunctionHandler(LambdaRequest request, ILambdaContext context)
    {
        var config = new AmazonDynamoDBConfig()
        {
            RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
        };

        var client = new AmazonDynamoDBClient(config);
        var tableName = Environment.GetEnvironmentVariable("target_table");

        var response = new LambdaResponse();
        string eventId = Guid.NewGuid().ToString();
        string createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";

        var document = new Document
        {
            ["id"] = eventId,
            ["principalId"] = request.PrincipalId,
            ["createdAt"] = createdAt,
            ["body"] = Document.FromJson(JsonConvert.SerializeObject(request.Content))
        };

        Table table = Table.LoadTable(client, tableName);
        await table.PutItemAsync(document);

        Event savedEvent = new Event
        {
            id = eventId,
            principalId = request.PrincipalId,
            createdAt = createdAt,
            body = request.Content
        };

        response.statusCode = 201;
        response.@event = savedEvent;

        return response;
    }
}

public class LambdaRequest
    {
        [JsonProperty("principalId")]
        public int PrincipalId { get; set; }

        [JsonProperty("content")]
        public Dictionary<string, string> Content { get; set; }
    }

    public class LambdaResponse
    {
        [JsonProperty("statusCode")]
        public int statusCode { get; set; }

        [JsonProperty("event")]
        public Event @event { get; set; }
    }

    public class Event
    {
        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("principalId")]
        public int principalId { get; set; }

        [JsonProperty("createdAt")]
        public string createdAt { get; set; }

        [JsonProperty("body")]
        public Dictionary<string, string> body { get; set; }
    }