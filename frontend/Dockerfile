FROM node:18-alpine

WORKDIR /app

# Copia os arquivos de configuração do npm
COPY package*.json ./

# Instala as dependências
RUN npm install

# Copia todo o código-fonte
COPY . .

# Faz o build de produção do Next.js
RUN npm run build

EXPOSE 3000

# Inicia a aplicação na porta 3000
CMD ["npm", "start"]
