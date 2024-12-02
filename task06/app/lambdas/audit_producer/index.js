const AWS = require('aws-sdk');
const { v4: uuidv4 } = require('uuid');

const dynamoDb = new AWS.DynamoDB.DocumentClient();

const AUDIT_TABLE_NAME = 'Audit';

exports.handler = async (event) => {
    const auditEntries = [];
    
    for (const record of event.Records) {
        if (record.eventName === 'INSERT') {

            const newItem = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.NewImage);
            auditEntries.push({
                id: uuidv4(),
                itemKey: newItem.key,
                modificationTime: new Date().toISOString(),
                newValue: newItem,
            });
        } else if (record.eventName === 'MODIFY') {

            const newItem = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.NewImage);
            const oldItem = AWS.DynamoDB.Converter.unmarshall(record.dynamodb.OldImage);

            for (const key of Object.keys(newItem)) {
                if (newItem[key] !== oldItem[key]) {
                    auditEntries.push({
                        id: uuidv4(),
                        itemKey: newItem.key,
                        modificationTime: new Date().toISOString(),
                        updatedAttribute: key,
                        oldValue: oldItem[key],
                        newValue: newItem[key],
                    });
                }
            }
        }
    }

    for (const entry of auditEntries) {
        try {
            await dynamoDb.put({
                TableName: AUDIT_TABLE_NAME,
                Item: entry,
            }).promise();
        } catch (error) {
            console.error('Failed to write to Audit table', error);
            throw error;
        }
    }

    console.log('Audit entries successfully created:', auditEntries);
};
