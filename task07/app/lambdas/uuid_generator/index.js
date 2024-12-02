const AWS = require('aws-sdk');
const { v4: uuidv4 } = require('uuid');
const s3 = new AWS.S3();

exports.handler = async (event) => {
    // cmtr-3a45b4b0-uuid-storage-test
    const bucketName = 'cmtr-3a45b4b0-uuid-storage-test'; // Specify your bucket name

    try {
        // Generate 10 random UUIDs
        const uuids = Array.from({ length: 10 }, () => uuidv4());

        // Prepare the content as a JSON string
        const fileContent = JSON.stringify({
            ids: uuids
        });

        // Get current ISO time for the file name
        const startTime = new Date();
        const isoString = startTime.toISOString().replace('Z', ''); // Remove 'Z' for microseconds
        const start = isoString.split('T')[0] + 'T'; // Date part in YYYY-MM-DDTHH
        const fileName = `${start}${startTime.getMilliseconds().toString().padStart(3, '0')}000Z.json`;  // Dynamic filename with milliseconds


        // Set up parameters for S3 upload
        const params = {
            Bucket: bucketName,
            Key: fileName,
            Body: fileContent,
            ContentType: 'application/json'
        };

        // Upload the file to S3
        const data = await s3.upload(params).promise();
        console.log(`File uploaded successfully at ${data.Location}`);

        return {
            statusCode: 200,
            body: JSON.stringify('UUIDs generated and uploaded as JSON successfully!'),
        };
    } catch (error) {
        console.error('Error uploading the UUIDs file as JSON:', error);
        return {
            statusCode: 500,
            body: JSON.stringify('Failed to generate and upload UUIDs as JSON.'),
        };
    }
};
