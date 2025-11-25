FROM golang:1.25-alpine3.22

RUN mkdir /app

RUN addgroup -S bankIndonesia
RUN adduser -S -D -h /app biuser bigroup
RUN chown -R biuser:bigroup /app

USER biuser

COPY main.go /app

EXPOSE 8080
CMD go run /app/main.go