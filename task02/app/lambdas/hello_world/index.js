exports.handler = async (event) => {
    // Extract the HTTP method and path from the event object
    const method = event.requestContext?.http?.method || event.httpMethod;
    const path = event.requestContext?.http?.path || event.path;
 
    if (path === "/hello" && method === "GET") {
        // Return success response for the valid endpoint
        return {
            statusCode: 200,
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify({
                statusCode: 200,
                message: "Hello from Lambda"
            }),
        };
    } else {
        return {
            statusCode: 400,
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify({
                statusCode: 400,
                message: "Bad request syntax or unsupported method. Request path: /cmtr-99c81425. HTTP method: GET"
            }),
        };
    }
};