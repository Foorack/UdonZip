var inflate = require('./tiny-inflate');

// var compressedBuffer = Buffer.from("tZQ7b8IwFIX3Sv0PkdeKGDpUVUVg6GNsGajU1bVvwKpfsi8U/n1vAmQAFJpGXSIl9jnn07mxx9ONNdkaYtLeFWyUD1kGTnql3aJg7/OXwT3LEgqnhPEOCraFxKaT66vxfBsgZaR2qWBLxPDAeZJLsCLlPoCjldJHK5Be44IHIb/EAvjtcHjHpXcIDgdYebDJ+AlKsTKYPW/o846E5Cx73O2rogomQjBaCiRQXq3ys7oIJrUI104d0Q32ZDkpa/O01CHd7BPeqJqoFWQzEfFVWOLgcpXQ2w9ruEaws+hDGuXtvGdifVlqCcrLlaUq8sa08oOIGloZSFcHc2qldzZUtStQg9AtW/oI3cMPfVfqzol19d0zz5b9y/BvHxVv5tR3zpUb1SwhJTpi1uSNsxXatf12NUdJJ2IuPs0fej/q4ASksb4IkQCR4FPvOZwwHJwvI+DWwH8A1L4X45HuOeD1s//Rr20Okby+Vyc/", "base64");
//var decompressedSize = 1432;
var compressedBuffer = Buffer.from("hc/BigIxDAbgu+A7lNydzngQkel4WRa8ibjgtXQyM8VpU5oo+vYWTyss7DEJ+f6k3T/CrO6Y2VM00FQ1KIyOeh9HAz/n79UWFIuNvZ0pooEnMuy75aI94WylLPHkE6uiRDYwiaSd1uwmDJYrShjLZKAcrJQyjzpZd7Uj6nVdb3T+bUD3YapDbyAf+gbU+ZlK8v82DYN3+EXuFjDKHxHa3VgoXMJ8zJS4yDaPKAa8YHi3mqrcC7pr9cd/3Qs=", "base64");
var decompressedSize = 737;
var outputBuffer = new Buffer(decompressedSize);

inflate(compressedBuffer, outputBuffer)

console.log(outputBuffer.toString("utf-8"));

