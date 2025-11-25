FROM alpine:3.22.2

LABEL author="Roni Purwanto"
LABEL company="NobleProg" email="ahmadroni@gmail.com"

RUN mkdir "hello"
RUN echo "Hello, Welcome to Docker" >> "hello/word.txt"

CMD cat "hello/word.txt"