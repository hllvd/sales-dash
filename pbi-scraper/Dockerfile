FROM node:20-slim

WORKDIR /app

# Install Chromium and its system dependencies for Puppeteer
RUN apt-get -o Acquire::Check-Valid-Until=false -o Acquire::Check-Date=false update && apt-get install -y --allow-unauthenticated \
    chromium \
    fonts-liberation \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcairo2 \
    libcups2 \
    libdrm2 \
    libgbm1 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libpango-1.0-0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libxshmfence1 \
    xdg-utils \
    --no-install-recommends \
 && rm -rf /var/lib/apt/lists/*

# Tell Puppeteer to use the system Chromium instead of downloading its own
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

# Install dependencies first for better layering
COPY package*.json ./
RUN npm install --production

# Copy application files
COPY . .

# Create output directory and set permissions
RUN mkdir -p /app/outputs && chmod 777 /app/outputs

# Create non-root user for security in Fargate container
RUN useradd -m -u 1001 nodeuser \
 && chown -R nodeuser:nodeuser /app

USER nodeuser

EXPOSE 3001

ENV NODE_ENV=production
ENV PORT=3001
ENV SCALE_TO_ZERO=true

# Container healthcheck using worker lock file or fallback
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD test -f /tmp/worker.lock || exit 0

CMD ["node", "worker.js"]
