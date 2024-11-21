exports.handler = async (event) => {
    const { path, method } =  event.requestContext.http;

    if (path === "/hello" && method === "GET") {
        return {
                statusCode: 200,
                message: "Hello from Lambda"
        };
    }

    return {
            statusCode: 400,
            message: "Bad request syntax or unsupported method. Request path: {path}. HTTP method: {method}"
    };
};
