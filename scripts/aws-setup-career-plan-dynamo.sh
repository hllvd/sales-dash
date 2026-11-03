#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Setup AWS DynamoDB table and IAM policy for SalesApp Career Plan Snapshots
# ==============================================================================

AWS_REGION="${AWS_REGION:-us-east-1}"
TABLE_NAME="${CAREER_PLAN_TABLE_NAME:-salesapp-career-plan-snapshots}"
POLICY_NAME="${CAREER_PLAN_POLICY_NAME:-SalesAppCareerPlanDynamoPolicy}"

echo "==> 1. Creating DynamoDB table: ${TABLE_NAME} (Region: ${AWS_REGION})..."

aws dynamodb create-table \
    --table-name "${TABLE_NAME}" \
    --attribute-definitions \
        AttributeName=PK,AttributeType=S \
        AttributeName=SK,AttributeType=S \
    --key-schema \
        AttributeName=PK,KeyType=HASH \
        AttributeName=SK,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --region "${AWS_REGION}" || echo "Table may already exist."

echo "==> Waiting for table to become ACTIVE..."
aws dynamodb wait table-exists --table-name "${TABLE_NAME}" --region "${AWS_REGION}"

echo "==> 2. Enabling Time to Live (TTL) on attribute 'TTL' (2 months expiration)..."
aws dynamodb update-time-to-live \
    --table-name "${TABLE_NAME}" \
    --time-to-live-specification "Enabled=true,AttributeName=TTL" \
    --region "${AWS_REGION}"

echo "==> 3. Creating IAM Policy: ${POLICY_NAME}..."

ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)

cat <<EOF > /tmp/career-plan-dynamo-policy.json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "SalesAppCareerPlanSnapshotsAccess",
            "Effect": "Allow",
            "Action": [
                "dynamodb:GetItem",
                "dynamodb:PutItem",
                "dynamodb:Query",
                "dynamodb:BatchWriteItem",
                "dynamodb:DescribeTable"
            ],
            "Resource": "arn:aws:dynamodb:${AWS_REGION}:${ACCOUNT_ID}:table/${TABLE_NAME}"
        }
    ]
}
EOF

aws iam create-policy \
    --policy-name "${POLICY_NAME}" \
    --policy-document file:///tmp/career-plan-dynamo-policy.json \
    --description "Permissions for SalesApp API to store and query Career Plan snapshots" || echo "Policy may already exist."

echo "==> Done! Attach '${POLICY_NAME}' to the IAM Role/User used by SalesApp API."
