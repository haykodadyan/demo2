// Import required AWS SDK and axios for HTTP requests
const AWS = require('aws-sdk');
const axios = require('axios');
const xray = require('aws-xray-sdk');

// Capturing all AWS clients
xray.captureAWS(require('aws-sdk'));

// Initialize DynamoDB Document Client
const dynamoDb = new AWS.DynamoDB.DocumentClient();

exports.handler = async (event) => {
    const url = "https://api.open-meteo.com/v1/forecast?latitude=52.52&longitude=13.41&current=temperature_2m,wind_speed_10m&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m";

    try {
        // Fetch data from Open-Meteo API
        const response = await axios.get(url);
        const weatherData = response.data;

        if (!weatherData) {
            return { statusCode: 400, body: 'No data received from API' };
        }

        const item = {
            id: AWS.util.uuid.v4(), // Generate a random UUID for ID
            forecast: {
                elevation: weatherData.elevation,
                generationtime_ms: weatherData.generationtime_ms,
                hourly: {
                    temperature_2m: weatherData.hourly.temperature_2m,
                    time: weatherData.hourly.time

                },
                hourly_units: {
                    temperature_2m: weatherData.hourly_units.temperature_2m,
                    time: weatherData.hourly_units.time

                },
                latitude: weatherData.latitude,
                longitude: weatherData.longitude,
                timezone: weatherData.timezone,
                timezone_abbreviation: weatherData.timezone_abbreviation,
                utc_offset_seconds: weatherData.utc_offset_seconds
            }
        };

        // Params object for DynamoDB
        const params = {
            TableName: 'cmtr-99c81425-Weather-test',
            Item: item,
        };

        // Insert data into DynamoDB
        await dynamoDb.put(params).promise();

        // Return success response
        return { statusCode: 200, body: JSON.stringify(item) };
    } catch (error) {
        console.error('Error fetching or storing data: ', error);
        return { statusCode: 500, body: 'Failed to fetch or store data' };
    }
};