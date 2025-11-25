FROM golang:1.25-alpine3.22

RUN mkdir app
COPY main.go app

EXPOSE 8080

CMD go run app/main.go