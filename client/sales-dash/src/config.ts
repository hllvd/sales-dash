const config = {
  apiUrl: process.env.NODE_ENV === 'production' ? '/api' : (process.env.REACT_APP_API_URL || 'http://localhost:5017/api'),
};

export default config;
