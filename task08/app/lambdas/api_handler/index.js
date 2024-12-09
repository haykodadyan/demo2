const https = require('https');

/**
 * Function to make an HTTP GET request to the Open-Meteo API.
 * @param {string} url - The API endpoint URL.
 * @returns {Promise<Object>} - The JSON response from the API.
 */
const fetchWeatherData = (url) => {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            let data = '';

            // Concatenate data chunks
            res.on('data', (chunk) => {
                data += chunk;
            });

            // On response end, parse and return the data
            res.on('end', () => {
                try {
                    const json = JSON.parse(data);
                    resolve(json);
                } catch (err) {
                    reject(`Error parsing JSON: ${err}`);
                }
            });
        }).on('error', (err) => {
            reject(`Request failed: ${err.message}`);
        });
    });
};

/**
 * AWS Lambda Handler
 * @param {Object} event - The Lambda event object.
 * @returns {Object} - API Gateway compatible response.
 */
exports.handler = async (event) => {
    const apiUrl = 'https://api.open-meteo.com/v1/forecast?latitude=52.52&longitude=13.41&current=temperature_2m,wind_speed_10m&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m';

    try {
        const weatherData = await fetchWeatherData(apiUrl);

        // Extract the hourly field from the weather data
        const hourlyData = weatherData.hourly;

        // Ensure the required structure in the response
        if (!hourlyData) {
            throw new Error("Hourly data is missing from the weather API response.");
        }

        return {
            statusCode: 200,
            body: JSON.stringify({
                message: 'Weather data fetched successfully.',
                hourly: {
                    time: hourlyData.time,
                    temperature_2m: hourlyData.temperature_2m,
                    relative_humidity_2m: hourlyData.relative_humidity_2m,
                    wind_speed_10m: hourlyData.wind_speed_10m,
                },
            }),
        };
    } catch (error) {
        return {
            statusCode: 500,
            body: JSON.stringify({
                message: 'Failed to fetch weather data.',
                error: error.message,
            }),
        };
    }
};
