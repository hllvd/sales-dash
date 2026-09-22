// infra/lambda/scraper-launcher/index.js
const { ECSClient, RunTaskCommand } = require('@aws-sdk/client-ecs');

const ecsClient = new ECSClient({ region: process.env.AWS_REGION || 'us-east-1' });

exports.handler = async (event) => {
  console.log('Recebida solicitação de disparo Fargate:', JSON.stringify(event));

  let body = {};
  if (event.body) {
    try {
      body = typeof event.body === 'string' ? JSON.parse(event.body) : event.body;
    } catch (err) {
      console.warn('Body não é JSON válido:', err.message);
    }
  }

  const workerCount = Math.min(Math.max(parseInt(body.workerCount || '1', 10), 1), 5);
  const useSpot = body.useSpot !== false;

  const cluster = process.env.ECS_CLUSTER || 'pbi-scraper-cluster';
  const taskDefinition = process.env.ECS_TASK_DEF || 'pbi-scraper-worker';
  const subnet = process.env.ECS_SUBNET; // Subnet padrão da AWS
  const securityGroup = process.env.ECS_SG; // Security group com egress 443 liberado

  if (!subnet) {
    console.error('ECS_SUBNET environment variable is required');
    return {
      statusCode: 500,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ error: 'ECS_SUBNET environment variable is missing in Lambda' })
    };
  }

  const runTaskParams = {
    cluster,
    taskDefinition,
    count: workerCount,
    launchType: useSpot ? undefined : 'FARGATE',
    capacityProviderStrategy: useSpot ? [
      { capacityProvider: 'FARGATE_SPOT', weight: 1 }
    ] : undefined,
    networkConfiguration: {
      awsvpcConfiguration: {
        subnets: [subnet],
        securityGroups: securityGroup ? [securityGroup] : undefined,
        assignPublicIp: 'ENABLED' // Permite saída direta para o AVA PRO sem NAT Gateway
      }
    }
  };

  try {
    console.log('Disparando ECS RunTask:', JSON.stringify(runTaskParams));
    const command = new RunTaskCommand(runTaskParams);
    const result = await ecsClient.send(command);

    const taskArns = (result.tasks || []).map(t => t.taskArn);
    console.log('Tasks iniciadas com sucesso:', taskArns);

    return {
      statusCode: 200,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        success: true,
        message: `${taskArns.length} task(s) iniciada(s) no Fargate`,
        taskArns,
        failures: result.failures || []
      })
    };
  } catch (err) {
    console.error('Erro ao executar RunTask no ECS:', err);
    return {
      statusCode: 500,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        success: false,
        error: err.message
      })
    };
  }
};
