const AWS = require('aws-sdk');
const { v4: uuidv4 } = require('uuid');
const dynamoDB = new AWS.DynamoDB.DocumentClient();

exports.handler = async (event) => {
    const auditTableName = 'cmtr-3a45b4b0-Audit-test';
    try {
        const auditEntries = [];

        for (const record of event.Records) {
            const eventName = record.eventName;

            if (eventName === 'INSERT') {
                const newImage = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.NewImage);

                const auditEntry = {
                    id: uuidv4(),
                    itemKey: newImage.key,
                    modificationTime: new Date().toISOString(),
                    newValue: newImage,
                };

                auditEntries.push(auditEntry);
            } else if (eventName === 'MODIFY') {

                const oldImage = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.OldImage);
                const newImage = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.NewImage);

                const updatedAttributes = findUpdatedAttributes(oldImage, newImage);

                for (const attr of updatedAttributes) {
                    const auditEntry = {
                        id: uuidv4(),
                        itemKey: newImage.key,
                        modificationTime: new Date().toISOString(),
                        updatedAttribute: attr,
                        oldValue: oldImage[attr],
                        newValue: newImage[attr],
                    };

                    auditEntries.push(auditEntry);
                }
            }
        }

        if (auditEntries.length > 0) {
            await batchWriteToDynamoDB(auditTableName, auditEntries);
        }

        console.log('Audit entries processed successfully.');
        return { statusCode: 200, body: 'Success' };
    } catch (error) {
        console.error('Error processing DynamoDB Stream event:', error);
        return { statusCode: 500, body: 'Error processing DynamoDB Stream event.' };
    }
};

function findUpdatedAttributes(oldItem, newItem) {
    return Object.keys(newItem).filter((key) => oldItem[key] !== newItem[key]);
}

async function batchWriteToDynamoDB(tableName, items) {
    const BATCH_SIZE = 25;
    const batches = [];

    for (let i = 0; i < items.length; i += BATCH_SIZE) {
        batches.push(items.slice(i, i + BATCH_SIZE));
    }

    for (const batch of batches) {
        const params = {
            RequestItems: {
                [tableName]: batch.map((item) => ({ PutRequest: { Item: item } })),
            },
        };
        await dynamoDB.batchWrite(params).promise();
    }
}
