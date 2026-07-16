const GA_TRACKING_ID = 'G-MYNY6WFJ13';

export const initGA = () => {
  // Only initialize Google Analytics in production
  if (process.env.NODE_ENV !== 'production') {
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
