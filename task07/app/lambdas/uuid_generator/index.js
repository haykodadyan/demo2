const AWS = require('aws-sdk');
const { v4: uuidv4 } = require('uuid');
const s3 = new AWS.S3();

exports.handler = async (event) => {

    const bucketName = 'cmtr-99c81425-uuid-storage-test';

    try {

        const uuids = Array.from({ length: 10 }, () => uuidv4());

        const fileContent = JSON.stringify({
            ids: uuids
        });

        const startTime = new Date();
        const isoString = startTime.toISOString().replace('Z', '');
        const start = isoString.split('T')[0] + 'T';
        const fileName = `${startTime.toISOString().replace('Z', '.000Z')}`;

        const params = {
            Bucket: bucketName,
            Key: fileName,
            Body: fileContent,
            ContentType: 'application/json'
        };

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
