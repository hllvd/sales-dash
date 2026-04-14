FROM node:20-alpine

WORKDIR /app

# Install dependencies first for better layering
COPY package*.json ./
RUN npm install --production

# Copy application files
COPY . .

# Create output directory and set permissions
RUN mkdir -p /app/outputs && chmod 777 /app/outputs

EXPOSE 3001

ENV NODE_ENV=production
ENV PORT=3001

CMD ["node", "server.js"]
