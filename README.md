# ðŸ“¦ Sistema de GestÃ£o de Pedidos

OlÃ¡! ðŸ‘‹ Bem-vindo ao repositÃ³rio. 

Desenvolvi este projeto focando em entregar um cÃ³digo resiliente, com arquitetura limpa e direcionado a resolver os problemas propostos no desafio, sem criar complexidades desnecessÃ¡rias. Abaixo, detalho como rodar o projeto localmente e compartilho o racional por trÃ¡s de cada decisÃ£o tÃ©cnica que tomei durante a construÃ§Ã£o.

## ðŸš€ Como Executar o Projeto

Como premissa de excelente ExperiÃªncia de Desenvolvimento (DX) aliada Ã  SeguranÃ§a, o projeto foi 100% "dockerizado" e utiliza arquivos `.env` para proteger segredos.

Na raiz do projeto, siga estes passos:

1. FaÃ§a uma cÃ³pia do arquivo de configuraÃ§Ã£o:
```bash
cp .env.example .env
```
*(Abra o arquivo `.env` recÃ©m-criado e cole a sua chave do Google Groq na variÃ¡vel `Groq_API_KEY`, se quiser testar o chat. As senhas de banco e pgAdmin jÃ¡ vÃªm preenchidas por padrÃ£o para uso local).*

2. Suba o ambiente:
```bash
docker-compose up -d --build
```

**O que esse comando faz?**
1. Sobe o **PostgreSQL** e o **RabbitMQ** aguardando os healthchecks.
2. Compila e sobe a **API (.NET 8)** na porta `5000` (rodando as *migrations* do banco automaticamente).
3. Compila e sobe o **Worker (.NET 8)** em background para ler as filas.
4. Compila e sobe o **Frontend (Next.js)** na porta `3000`.

### ðŸ”— Links de Acesso Ãšteis
- **Frontend (Painel Principal):** [http://localhost:3000](http://localhost:3000)
- **Painel de Mensageria (RabbitMQ):** [http://localhost:15672](http://localhost:15672) *(User: `guest` | Pass: `guest`)*
- **Painel de Telemetria (Aspire Dashboard):** [http://localhost:18888](http://localhost:18888) *(Tracing e logs dos containers)*
- **Painel do Banco de Dados (pgAdmin):** [http://localhost:5050](http://localhost:5050)
  - **Login:** *(Veja as variÃ¡veis `PGADMIN_EMAIL` e `PGADMIN_PASS` no seu arquivo `.env`)*
  - *Dica para conectar o banco:* Dentro do painel, clique em *Add New Server*. Na aba 'Connection', preencha Hostname: `postgres`, Port: `5432`, e as credenciais definidas em `DB_USER` e `DB_PASS` no seu arquivo `.env`.

### ðŸ§ª Como rodar a SuÃ­te de Testes
Este projeto possui Testes de IntegraÃ§Ã£o usando **Testcontainers** (bancos reais efÃªmeros no Docker). Em um terminal na raiz, rode:
```bash
cd backend
dotnet test
```

---

## ðŸ“ Arquitetura do Sistema

Para facilitar a visualizaÃ§Ã£o de como os componentes se comunicam, desenhei este diagrama simples da nossa topologia:

```mermaid
flowchart LR
  %% Componentes
  UI["ðŸ–¥ï¸ Dashboard UI (Next.js)"]
  Chat["ðŸ¤– Chat IA (Next.js)"]
  
  API["âš™ï¸ Orders.API (.NET)"]
  Worker["âš™ï¸ Orders.Worker (.NET)"]
  
  DB[(ðŸ—„ï¸ PostgreSQL)]
  MQ[[ðŸ‡ RabbitMQ]]
  Groq["ðŸ§  Google Groq"]

  %% Fluxo 1: CriaÃ§Ã£o e Processamento (SÃ³lido)
  UI == "1. POST /orders" ==> API
  API -- "2. Salva Pedido+Outbox" --> DB
  API -- "3. LÃª e Publica" --> MQ
  MQ -- "4. Fila (Consumo)" --> Worker
  Worker -- "5. Atualiza Status" --> DB

  %% Fluxo 2: IA Text-to-SQL (Pontilhado)
  Chat -. "A. Pergunta Natural" .-> API
  API -. "B. Gera SQL" .-> Groq
  API -. "C. Roda Query" .-> DB
  API -. "D. Humaniza Resposta" .-> Groq
  API -. "E. Exibe Texto" .-> Chat
```

---

## ðŸ§  Por que tomei essas decisÃµes tÃ©cnicas?

Nesse trecho justifico minhas escolhas tÃ©cnicas focando no que realmente resolve o problema de negÃ³cio de forma escalÃ¡vel e de acordo com o prazo.

### 1. O Problema do "Dual-Write" e a escolha do RabbitMQ
Eu precisava salvar o pedido no banco de dados e avisar os outros sistemas via mensageria. Como garantir que a rede nÃ£o falhe no milissegundo exato entre salvar no banco e enviar para a fila? 
Eu decidi usar o **Outbox Pattern**. Eu salvo o Pedido e a Mensagem de evento na mesma *transaÃ§Ã£o ACID* do Entity Framework no PostgreSQL. Depois, um `BackgroundService` da prÃ³pria API lÃª a tabela de tempos em tempos e publica na fila. 

### 2. O Worker e a IdempotÃªncia
Trabalhar com sistemas distribuÃ­dos Ã© assumir como verdade absoluta que a rede vai falhar ou duplicar mensagens. Para blindar o sistema, fiz o meu serviÃ§o `Orders.Worker` ser extremamente defensivo. Sempre que ele pega uma mensagem, ele vai ao banco de dados e confere o status real. Se nÃ£o estiver mais como `Pendente`, ele encerra o processo (IdempotÃªncia). Amarrei a regra no cÃ³digo para garantir matematicamente o ciclo estrito: *Pendente â†’ Processando â†’ Finalizado*.

### 3. Tempo Real no Frontend
O desafio pedia atualizaÃ§Ã£o de status em tempo real com fallback. Eu poderia ter plugado um servidor SignalR (WebSockets), mas pesando o custo x benefÃ­cio e o tempo do desafio, optei por uma soluÃ§Ã£o muito mais enxuta: **TanStack React Query** no Next.js com um `refetchInterval` de 3 segundos (*Short Polling*). Funciona de forma fluida, cobre a exigÃªncia nativamente e economiza infraestrutura de conexÃµes abertas no servidor. 

### 4. InteligÃªncia Artificial
O MÃ³dulo de IA tem foco na arquitetura corporativa: apliquei o *Dependency Inversion Principle* (SOLID) e criei uma interface genÃ©rica (`IAiAnalyticsService`). A API nÃ£o sabe qual LLM estÃ¡ respondendo. Se no futuro for preciso assinar e usar o GPT-4 da OpenAI, basta plugar a classe correspondente e injetar o serviÃ§o no `Program.cs`, sem encostar em regra de negÃ³cio ou ferir os doze fatores da aplicaÃ§Ã£o.

### 5. Testes de IntegraÃ§Ã£o reais
Testar consultas de banco e filas simulando eles na memÃ³ria causa falsos positivos. Para testar a infraestrutura de fato, utilizei o **Testcontainers**. A cada execuÃ§Ã£o dos meus testes, ele sobe um container efÃªmero no Docker, testa a API salvando no PostgreSQL real com RabbitMQ real, e destrÃ³i o ambiente depois. TambÃ©m apliquei o padrÃ£o de **Golden Tests** (Snapshot usando `Verify.Xunit`) travando os contratos da API.


## Listando seus Modelos do Groq
Você pode listar todos os modelos suportados na sua conta do Groq usando o comando cURL abaixo (útil se algum modelo gigante entrar em cooldown):
`ash
curl -X GET "https://api.groq.com/openai/v1/models" -H "Authorization: Bearer $GROQ_API_KEY"
`

## Arquitetura Limpa e Open-Closed Principle (OCP)
A funcionalidade de Inteligência Artificial do projeto foi desenhada visando o Princípio de Inversão de Dependência (DIP) e o Princípio do Aberto/Fechado (OCP). Se no futuro você desejar mudar o provedor (por exemplo, usar OpenAI ou Gemini), basta criar uma nova classe e registrá-la no Program.cs. 

Exemplo:
`csharp
public class ClaudeAiService : IAiAnalyticsService
{
    public async Task<string> AskAboutOrdersAsync(string userQuestion)
    {
        // Lógica de chamadas à Anthropic (Claude) aqui...
    }
}
`
E no Program.cs:
`csharp
builder.Services.AddHttpClient<IAiAnalyticsService, ClaudeAiService>();
`
Zero modificação nos Controllers ou nas regras de negócio!
