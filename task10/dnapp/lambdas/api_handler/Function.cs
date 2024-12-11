using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using AttributeType = Amazon.CognitoIdentityProvider.Model.AttributeType;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SimpleLambdaFunction;

public class Function
{
    private readonly ApiHandler _apiHandler;

    public Function()
    {
        _apiHandler = new ApiHandler();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request == null)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = $"Request is null. request: {request} context {context}"
            };
        }

        if (context == null)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = $"Lambda context is null.  request: {request} context {context}"
            };
        }

        return await _apiHandler.HandleRequest(request, context);
    }
}

public class ApiHandler
{
    private readonly AuthenticationService _authService;
    private readonly Table _tablesTable;
    private readonly Table _reservationsTable;


    public ApiHandler()
    {
        _authService = new AuthenticationService();

        var config = new AmazonDynamoDBConfig()
        {
            RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
        };
        var dynamoDbClient = new AmazonDynamoDBClient(config);

        _tablesTable = Table.LoadTable(dynamoDbClient, Environment.GetEnvironmentVariable("tables_table"));
        _reservationsTable = Table.LoadTable(dynamoDbClient, Environment.GetEnvironmentVariable("reservations_table"));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest eventRequest, ILambdaContext context)
    {
        if (eventRequest == null || string.IsNullOrEmpty(eventRequest.Resource) || string.IsNullOrEmpty(eventRequest.HttpMethod))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = $"Invalid request. Resource or HTTP Method is missing. Event request {eventRequest}. Context {context}"
            };
        }

        var requestPath = eventRequest.Resource;
        var methodName = eventRequest.HttpMethod;

        var actionEndpointMapping =
            new Dictionary<string, Dictionary<string, Func<APIGatewayProxyRequest, Task<APIGatewayProxyResponse>>>>()
            {
                    { "/signup", new() { { "POST", Signup } } },
                    { "/signin", new() { { "POST", Signin } } },
                    { "/tables", new() { { "GET", GetTables }, { "POST", AddTable } } },
                    { "/tables/{tableId}", new() { { "GET", GetTableById } } },
                    { "/reservations", new() { { "GET", GetReservations }, { "POST", AddReservation } } }
            };

        if (!actionEndpointMapping.TryGetValue(requestPath, out var resourceMethods) ||
            !resourceMethods.TryGetValue(methodName, out var action))
        {
            return InvalidEndpoint(requestPath, methodName);
        }

        return await action(eventRequest);
    }

    private APIGatewayProxyResponse InvalidEndpoint(string path, string method)
    {
        return FormatResponse(400, new { message = $"Unsupported path or method: {path} {method}" });
    }

    private APIGatewayProxyResponse FormatResponse(int code, object response)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = code,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
            Body = JsonSerializer.Serialize(response)
        };
    }

    private async Task<APIGatewayProxyResponse> Signup(APIGatewayProxyRequest request)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Body ?? "{}");
        var requiredParams = new[] { "firstName", "lastName", "email", "password" };
        var missingParams = ValidateRequestParams(requiredParams, body);

        if (missingParams.Count > 0)
        {
            return FormatResponse(400, new { message = $"Missing required parameters: {string.Join(", ", missingParams)}" });
        }

        var firstName = body["firstName"].GetString();
        var lastName = body["lastName"].GetString();
        var email = body["email"].GetString();
        var password = body["password"].GetString();

        try
        {
            await _authService.SignUp(firstName, lastName, email, password);
            return FormatResponse(200, new { message = $"User {email} signed up successfully" });
        }
        catch (Exception ex)
        {
            return FormatResponse(400, new { message = ex.Message });
        }
    }

    private async Task<APIGatewayProxyResponse> Signin(APIGatewayProxyRequest request)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Body ?? "{}");
        var requiredParams = new[] { "email", "password" };
        var missingParams = ValidateRequestParams(requiredParams, body);

        if (missingParams.Count > 0)
        {
            return FormatResponse(400, new { message = $"Missing required parameters: {string.Join(", ", missingParams)}" });
        }

        var email = body["email"].GetString();
        var password = body["password"].GetString();

        try
        {
            var token = await _authService.SignIn(email, password);
            return FormatResponse(200, new { accessToken = token });
        }
        catch (Exception ex)
        {
            return FormatResponse(400, new { message = ex.Message + $" Request {request}" });
        }
    }

    private async Task<APIGatewayProxyResponse> GetTables(APIGatewayProxyRequest request)
    {
        var scanResult = await _tablesTable.Scan(new ScanOperationConfig()).GetRemainingAsync();
        var tables = scanResult.Select(item => new
        {
            id = item["id"].AsInt(),
            number = item["number"].AsInt(),
            places = item["places"].AsInt(),
            isVip = item["isVip"].AsBoolean(),
            minOrder = item.ContainsKey("minOrder") ? item["minOrder"].AsInt() : (int?)null
        });

        return FormatResponse(200, new { tables });
    }

    private async Task<APIGatewayProxyResponse> AddTable(APIGatewayProxyRequest request)
    {
        try
        {
            Dictionary<string, JsonElement> body;
            try
            {
                body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Body ?? "{}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during deserialization: " + ex.Message);
                return FormatResponse(400, new { message = "Invalid request body format." });
            }

            var requiredParams = new[] { "id", "number", "places", "isVip" };

            var missingParams = ValidateRequestParams(requiredParams, body);
            if (missingParams.Count > 0)
            {
                return FormatResponse(400, new { message = $"Missing required parameters: {string.Join(", ", missingParams)}" });
            }

            int id, number, places;
            bool isVip;
            int? minOrder = null;

            try
            {
                id = body["id"].GetInt32();
                number = body["number"].GetInt32();
                places = body["places"].GetInt32();
                isVip = body["isVip"].GetBoolean();

                if (body.ContainsKey("minOrder"))
                {
                    minOrder = body["minOrder"].GetInt32();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing fields: " + ex.Message);
                return FormatResponse(400, new { message = "Invalid field values." });
            }

            var items = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = id.ToString() } },
                    { "number", new AttributeValue { N = number.ToString() } },
                    { "places", new AttributeValue { N = places.ToString() } },
                    { "isVip", new AttributeValue { S = isVip.ToString() } }
                };

            if (minOrder.HasValue)
            {
                items.Add("minOrder", new AttributeValue { N = minOrder.ToString() });
            }

            var putItemRequest = new PutItemRequest
            {
                TableName = Environment.GetEnvironmentVariable("tables_table"),
                Item = items
            };

            try
            {
                var config = new AmazonDynamoDBConfig()
                {
                    RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
                };
                var dynamoDbClient = new AmazonDynamoDBClient(config);

                await dynamoDbClient.PutItemAsync(putItemRequest);
            }
            catch (Exception ex)
            {
                return FormatResponse(500, new { message = "Error saving data to the database.", error = ex.Message });
            }

            var response = new { id = id };
            return FormatResponse(200, response);
        }
        catch (Exception ex)
        {
            return FormatResponse(500, new { message = "Other error occurred.", error = ex.Message });
        }
    }

    private async Task<APIGatewayProxyResponse> GetTableById(APIGatewayProxyRequest request)
    {
        var tableIdStr = request.PathParameters?["tableId"];
        if (string.IsNullOrEmpty(tableIdStr) || !int.TryParse(tableIdStr, out var tableId))
        {
            return FormatResponse(400, new { message = "Invalid or missing tableId" });
        }

        var table = await _tablesTable.GetItemAsync(tableId.ToString());
        if (table == null)
        {
            return FormatResponse(404, new { message = $"Table with id {tableId} not found" });
        }

        var response = new
        {
            id = table["id"].AsInt(),
            number = table["number"].AsInt(),
            places = table["places"].AsInt(),
            isVip = table["isVip"].AsBoolean(),
            minOrder = table.ContainsKey("minOrder") ? table["minOrder"].AsInt() : (int?)null
        };

        return FormatResponse(200, response);
    }

    private async Task<APIGatewayProxyResponse> GetReservations(APIGatewayProxyRequest request)
    {
        var scanResult = await _reservationsTable.Scan(new ScanOperationConfig()).GetRemainingAsync();
        var reservations = scanResult.Select(item => new
        {
            tableNumber = item["tableNumber"].AsInt(),
            clientName = item["clientName"].AsString(),
            phoneNumber = item["phoneNumber"].AsString(),
            date = item["date"].AsString(),
            slotTimeStart = item["slotTimeStart"].AsString(),
            slotTimeEnd = item["slotTimeEnd"].AsString()
        });

        return FormatResponse(200, new { reservations });
    }

    private async Task<APIGatewayProxyResponse> AddReservation(APIGatewayProxyRequest request)
    {
        try
        {
            Dictionary<string, JsonElement> body;
            try
            {
                body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Body ?? "{}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during deserialization: " + ex.Message);
                return FormatResponse(400, new { message = "Invalid request body format." });
            }

            var requiredParams = new[] { "tableNumber", "clientName", "phoneNumber", "date", "slotTimeStart", "slotTimeEnd" };

            var missingParams = ValidateRequestParams(requiredParams, body);
            if (missingParams.Count > 0)
            {
                return FormatResponse(400, new { message = $"Missing required parameters: {string.Join(", ", missingParams)}" });
            }

            var reservationId = Guid.NewGuid().ToString();

            int tableNumber;
            string clientName, phoneNumber, date, slotTimeStart, slotTimeEnd;

            try
            {
                tableNumber = body["tableNumber"].GetInt32();
                clientName = body["clientName"].GetString();
                phoneNumber = body["phoneNumber"].GetString();
                date = body["date"].GetString();
                slotTimeStart = body["slotTimeStart"].GetString();
                slotTimeEnd = body["slotTimeEnd"].GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing fields: " + ex.Message);
                return FormatResponse(400, new { message = "Invalid field values." });
            }

            var scanRequest = new ScanRequest
            {
                TableName = Environment.GetEnvironmentVariable("tables_table"),
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#t", "number" }
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":number", new AttributeValue { N = tableNumber.ToString() } }
                },
                FilterExpression = "#t = :number"
            };

            try
            {
                var config = new AmazonDynamoDBConfig()
                {
                    RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
                };
                var dynamoDbClient = new AmazonDynamoDBClient(config);
                var scanResponse = await dynamoDbClient.ScanAsync(scanRequest);

                if (scanResponse.Items == null || scanResponse.Items.Count == 0)
                {
                    return FormatResponse(400, new { message = $"Table with number {tableNumber} does not exist." });
                }
            }
            catch (Exception ex)
            {
                return FormatResponse(400, new { message = "Error checking table existence.", error = ex.Message });
            }


            //////////////////////////////////
            var scanResult2 = await _reservationsTable.Scan(new ScanOperationConfig()).GetRemainingAsync();
            foreach (var item in scanResult2)
            {
                int existingTableNumber = int.Parse(item["tableNumber"].AsString());
                var existingSlotStart = item["slotTimeStart"].AsString();
                var existingSlotEnd = item["slotTimeEnd"].AsString();

                if (IsOverlapping(tableNumber, slotTimeStart, slotTimeEnd, existingTableNumber, existingSlotStart, existingSlotEnd))
                {
                    return FormatResponse(400, new { message = "The reservation overlaps with an existing reservation for this table. slotTimeStart: " + slotTimeStart + ", slotTimeEnd: " + slotTimeEnd + ", existingSlotStart: " + existingSlotStart + ", existingSlotEnd " + existingSlotEnd });
                }
            }
            //////////////////////////////////
            var reservationItem = new Dictionary<string, AttributeValue>
                {
                    { "id", new AttributeValue { S = reservationId} },
                    { "tableNumber", new AttributeValue { N = tableNumber.ToString() } },
                    { "clientName", new AttributeValue { S = clientName } },
                    { "phoneNumber", new AttributeValue { S = phoneNumber } },
                    { "date", new AttributeValue { S = date } },
                    { "slotTimeStart", new AttributeValue { S = slotTimeStart } },
                    { "slotTimeEnd", new AttributeValue { S = slotTimeEnd } }
                };

            var putItemRequest = new PutItemRequest
            {
                TableName = Environment.GetEnvironmentVariable("reservations_table"),
                Item = reservationItem
            };

            try
            {
                var config = new AmazonDynamoDBConfig()
                {
                   RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
                };
                var dynamoDbClient = new AmazonDynamoDBClient(config);

                await dynamoDbClient.PutItemAsync(putItemRequest);
            }
            catch (Exception ex)
            {
                return FormatResponse(400, new { message = "Error saving reservation to the database.", error = ex.Message });
            }

            var response = new { reservationId };
            return FormatResponse(200, response);
        }
        catch (Exception ex)
        {
            return FormatResponse(400, new { message = "Other error occurred.", error = ex.Message });
        }
    }

    private bool IsOverlapping(int newTableNumber, string newStart, string newEnd, int existingTableNumber, string existingStart, string existingEnd)
    {
        if (newTableNumber != existingTableNumber)
        {
            return false;
        }

        if (string.IsNullOrEmpty(existingStart) || string.IsNullOrEmpty(existingEnd))
        {
            return false;
        }

        if (!DateTime.TryParse(newStart, out DateTime newStartDate) ||
            !DateTime.TryParse(newEnd, out DateTime newEndDate) ||
            !DateTime.TryParse(existingStart, out DateTime existingStartDate) ||
            !DateTime.TryParse(existingEnd, out DateTime existingEndDate))
        {
            throw new FormatException("One or more date strings are invalid.");
        }

        if (newStartDate < existingEndDate && newEndDate > existingStartDate)
        {
            if (newEndDate == existingStartDate || newStartDate == existingEndDate)
            {
                return false; // Allow back-to-back reservations but not overlapping
            }
            return true;
        }
        return false;
    }


        private List<string> ValidateRequestParams(string[] expected, Dictionary<string, JsonElement> received)
        {
            return expected.Where(param => !received.ContainsKey(param)).ToList();
        }
    }

public class AuthenticationService
{
    private readonly AmazonCognitoIdentityProviderClient _cognitoClient;
    private readonly string _clientId = Environment.GetEnvironmentVariable("cup_client_id");
    private readonly string _userPoolId = Environment.GetEnvironmentVariable("cup_id");

    public AuthenticationService()
    {
        _cognitoClient = new AmazonCognitoIdentityProviderClient();
    }

    public async Task SignUp(string firstName, string lastName, string email, string password)
    {
        var signUpRequest = new SignUpRequest
        {
            ClientId = _clientId,
            Username = email,
            Password = password,
            UserAttributes = new List<AttributeType>
                {
                    new AttributeType { Name = "given_name", Value = firstName },
                    new AttributeType { Name = "family_name", Value = lastName },
                    new AttributeType { Name = "email", Value = email }
                }
        };

        var signUpResponse = await _cognitoClient.SignUpAsync(signUpRequest);

        var confirmSignUpRequest = new AdminConfirmSignUpRequest
        {
            Username = email,
            UserPoolId = _userPoolId
        };

        await _cognitoClient.AdminConfirmSignUpAsync(confirmSignUpRequest);
    }

    public async Task<string> SignIn(string email, string password)
    {
        var authRequest = new InitiateAuthRequest
        {
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            ClientId = _clientId,
            AuthParameters = new Dictionary<string, string>
                {
                    { "USERNAME", email },
                    { "PASSWORD", password }
                }
        };

        var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);
        return authResponse.AuthenticationResult.IdToken;
    }
}
