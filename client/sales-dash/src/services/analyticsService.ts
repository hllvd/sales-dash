const GA_TRACKING_ID = process.env.REACT_APP_GA_TRACKING_ID;

export const initGA = () => {
  // Only initialize Google Analytics if the tracking ID environment variable is supplied
  if (!GA_TRACKING_ID) {
    return;
  }

  // Inject tracking script
  const script = document.createElement('script');
  script.async = true;
  script.src = `https://www.googletagmanager.com/gtag/js?id=${GA_TRACKING_ID}`;
  document.head.appendChild(script);

  // Initialize dataLayer and gtag function
  (window as any).dataLayer = (window as any).dataLayer || [];
  (window as any).gtag = function () {
    (window as any).dataLayer.push(arguments);
  };

  (window as any).gtag('js', new Date());
  (window as any).gtag('config', GA_TRACKING_ID, { send_page_view: false });
};
