import { AwsRum, AwsRumConfig } from 'aws-rum-web';

let awsRum: AwsRum | null = null;

export const initRUM = () => {
  try {
    const config: AwsRumConfig = {
      sessionSampleRate: 1,
      identityPoolId: process.env.REACT_APP_AWS_RUM_IDENTITY_POOL_ID,
      endpoint: `https://dataplane.rum.${process.env.REACT_APP_AWS_REGION}.amazonaws.com`,
      telemetries: ["errors", "http", "performance"],
      allowCookies: true,
      enableXRay: false,
      signing: true
    };

    const APPLICATION_ID = process.env.REACT_APP_AWS_RUM_APP_ID || '';
    const APPLICATION_VERSION = '1.0.0';
    const APPLICATION_REGION = process.env.REACT_APP_AWS_REGION || 'us-east-1';

    if (APPLICATION_ID) {
      awsRum = new AwsRum(
        APPLICATION_ID,
        APPLICATION_VERSION,
        APPLICATION_REGION,
        config
      );
      console.log('AWS RUM initialized successfully');
    }
  } catch (error) {
    console.error('Failed to initialize AWS RUM', error);
  }
};

export const recordError = (error: Error) => {
  if (awsRum) {
    awsRum.recordError(error);
  }
};
