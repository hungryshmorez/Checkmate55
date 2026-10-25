# CHECKMATE 55 room server (Co-op Escape and Driverless Duel). Build from the REPOSITORY ROOT:
#   docker build -f Tools/Server/Dockerfile -t checkmate55-rooms .
#   docker run -p 8080:8080 checkmate55-rooms
FROM node:22-alpine
WORKDIR /app
COPY Tools/Chess ./Tools/Chess
COPY Tools/Server/package.json Tools/Server/package-lock.json ./Tools/Server/
WORKDIR /app/Tools/Server
RUN npm ci --omit=dev
COPY Tools/Server/room.cjs Tools/Server/server.cjs ./
ENV PORT=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/health || exit 1
USER node
CMD ["node","server.cjs"]
