exports.handler = async (event) => {
    console.log("Received SQS message:", JSON.stringify(event, null, 2));
    return {
        statusCode: 200,
        body: JSON.stringify('Message processed successfully')
    };
};
