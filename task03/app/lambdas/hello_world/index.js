exports.handler = async (event) => {
    console.log("Event received:", JSON.stringify(event, null, 2));
    console.log("Executing Lambda function...");

    const response = {
        statusCode: 200,
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            "statusCode": 200,
            "message": "Hello from Lambda"
        })
    };
    return response;
};