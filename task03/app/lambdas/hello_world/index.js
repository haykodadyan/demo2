exports.handler = async (event) => {
    console.log("Event received:", JSON.stringify(event, null, 2)); // Logs the input event
    console.log("Executing Lambda function...");
    // Construct the correct response
    const response = {
        statusCode: 200, // HTTP status code
        headers: {
            "Content-Type": "application/json"
        }, // Optional headers
        body: JSON.stringify({
            "statusCode": 200,
            "message": "Hello from Lambda"
        })
    };
    return response;
};
