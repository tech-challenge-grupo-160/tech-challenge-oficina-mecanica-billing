# Mercado Pago: aplicação, contas de teste e credenciais

Referência da [F4-06](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/issues/137).
Explica qual produto do Mercado Pago o Billing usa, onde ficam as credenciais e
como qualquer integrante do grupo gera tudo de novo do zero.

> **Regra única:** access token e assinatura do webhook **nunca** entram no
> repositório, em issue, em PR, em print ou em log. O lugar deles é o
> AWS Secrets Manager, alimentado pelos secrets do GitHub.

## Produto escolhido: Checkout Pro

O Billing cria uma **preferência de pagamento** com os itens do orçamento
aprovado e devolve o link (`init_point`) para o cliente pagar na página do
Mercado Pago. A confirmação chega por **webhook** do tópico `payment`.

Motivos da escolha:

- o cliente paga na página do Mercado Pago, então o Billing não recebe nem
  guarda dados de cartão;
- Pix, cartão e saldo em conta vêm prontos na mesma página, sem uma integração
  para cada meio;
- é o fluxo mais curto para testar ponta a ponta com contas de teste.

Pix via API (Checkout Transparente) foi descartado nesta fase: exigiria montar
o QR code no front e tratar cada meio de pagamento separadamente.

Documentação oficial:

| Assunto | Link |
|---|---|
| Checkout Pro (visão geral) | https://www.mercadopago.com.br/developers/pt/docs/checkout-pro/landing |
| Checkout Pro com Preferences API | https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-preferences/overview |
| Credenciais | https://www.mercadopago.com.br/developers/pt/docs/your-integrations/credentials |
| Contas de teste | https://www.mercadopago.com.br/developers/pt/docs/your-integrations/test/accounts |
| Webhooks e assinatura `x-signature` | https://www.mercadopago.com.br/developers/pt/docs/your-integrations/notifications/webhooks |

## Onde ficam as credenciais

Um segredo por ambiente no AWS Secrets Manager, criado pelo Terraform do
[tech-challenge-infra-k8s](https://github.com/tech-challenge-grupo-160/tech-challenge-infra-k8s)
(`infra/mercadopago.tf`):

| Ambiente | Segredo |
|---|---|
| dev | `tc-grupo160/dev/mercadopago` |
| hom | `tc-grupo160/hom/mercadopago` |
| prod | `tc-grupo160/prod/mercadopago` |

Conteúdo (JSON):

```json
{
  "access_token": "<access token da aplicação>",
  "webhook_secret": "<assinatura secreta do webhook>"
}
```

O valor chega ao Terraform pelos secrets do repositório `tech-challenge-infra-k8s`
no GitHub (*Settings → Secrets and variables → Actions*):

| Secret do GitHub | Vai para |
|---|---|
| `MERCADOPAGO_ACCESS_TOKEN_DEV` / `_HOM` / `_PROD` | `access_token` do segredo do ambiente |
| `MERCADOPAGO_WEBHOOK_SECRET_DEV` / `_HOM` / `_PROD` | `webhook_secret` do segredo do ambiente |

Sem os secrets, o segredo é criado **sem valor**. O plan e o apply continuam
passando, e o Billing deve falhar no startup com uma mensagem clara.

Todos os ambientes do lab usam **credenciais de teste**: nenhum pagamento real
passa pelo projeto. Os ambientes podem compartilhar a mesma aplicação, mas cada
um precisa da sua URL de webhook (ver passo 4).

### Como o Billing deve ler (F4-20)

Mesmo padrão da chave do JWT no OS Service (`JwtSigningKeyResolver`):

- no cluster, a configuração recebe só o **nome** do segredo
  (`MercadoPago:SecretId`), e o serviço lê o valor no Secrets Manager no startup.
  Os nodes do EKS usam a `LabRole` (o Learner Lab não permite criar roles), e a
  VPC tem endpoint de interface para o Secrets Manager;
- localmente, `MercadoPago:AccessToken` e `MercadoPago:WebhookSecret` vêm de
  `dotnet user-secrets` ou de variáveis de ambiente (`MercadoPago__AccessToken`),
  nunca do `appsettings.json`;
- trocar o valor do segredo exige reiniciar os pods
  (`kubectl rollout restart deployment/oficina-mecanica-billing -n oficina-mecanica`).

## Passo a passo para gerar as credenciais

Leva uns 15 minutos. Qualquer integrante com conta no Mercado Pago consegue
fazer.

### 1. Criar a aplicação

1. Entre em https://www.mercadopago.com.br/developers/panel/app com a sua conta
   do Mercado Pago.
2. Clique em **Criar aplicação**.
3. Dê um nome (ex.: `oficina-mecanica-grupo160`), escolha **Pagamentos online**
   e o produto **Checkout Pro**.
4. Confirme. A aplicação já nasce com **credenciais de teste** e uma conta de
   teste criada automaticamente.

### 2. Criar as contas de teste (vendedor e comprador)

1. Dentro da aplicação, no menu lateral, abra **Contas de teste**.
2. Clique em **Criar conta de teste**:
   - país **Brasil** (não muda depois, e vendedor e comprador precisam ser do
     mesmo país);
   - identificação `Vendedor`, tipo **Vendedor**.
3. Repita para o comprador: identificação `Comprador`, tipo **Comprador**, com
   algum saldo fictício.
4. Guarde usuário e senha de cada uma no gerenciador de senhas do grupo, não no
   repositório. O comprador de teste é quem faz login na página do Checkout Pro
   para simular o pagamento.

O limite é de 15 contas de teste por aplicação.

### 3. Copiar o access token

1. Na aplicação, abra **Credenciais** e selecione o ambiente **Teste**.
2. Copie o **Access Token**. Nessas integrações ele começa com `APP_USR-`.
3. Não ative as credenciais de produção: o projeto não processa pagamento real.

### 4. Configurar o webhook e gerar a assinatura secreta

1. Na aplicação, abra **Webhooks → Configurar notificações**.
2. Em **URL modo teste**, informe a URL pública do Billing no ambiente
   (`<gateway_url>/api/v1/pagamentos/webhook`, ver "URL definitiva" abaixo). Se o
   Billing ainda não estiver no ar, use uma URL provisória e atualize depois. A
   assinatura não muda.
3. Em **Eventos recomendados para integrações com Checkout Pro**, marque só
   **Pagamentos (legacy)** (tópico `payment`). Em **Outros eventos**, deixe tudo
   desmarcado: `Order (Mercado Pago)` é da Orders API e `Planos e assinaturas` é
   de cobrança recorrente, e o Billing não usa nenhum dos dois.
4. Clique em **Salvar**. O Mercado Pago gera a **assinatura secreta** da
   aplicação.
5. Revele e copie a assinatura. Ela não expira. Se vazar, use o botão de
   redefinir ao lado dela e refaça os passos 5 e 6.

O Billing usa essa assinatura para validar o header `x-signature` de cada
notificação, e deve responder `200` em até 22 segundos, senão o Mercado Pago
reenvia.

#### URL provisória para testar

Enquanto o Billing não estiver no ar no ambiente, use o https://webhook.site:

- no painel, cole a **"Your unique URL"**: `https://webhook.site/<id>`;
- **não** use o endereço da barra do navegador (`https://webhook.site/#!/view/<id>`).
  Ele é só a tela de visualização e não recebe nada;
- salve, clique em **Simular notificação** (URL de teste, evento Pagamentos,
  qualquer ID) e veja o `POST` chegar na página de visualização, com os headers
  `x-signature` e `x-request-id` e o corpo com `"type": "payment"` e `data.id`.

A URL do webhook.site é pública para quem tiver o link: não compartilhe.

#### URL definitiva

A URL definitiva é o Billing exposto pelo API Gateway do ambiente:

```text
<gateway_url>/api/v1/pagamentos/webhook
```

- `<gateway_url>` é o output `gateway_url` do Terraform do `tech-challenge-infra-k8s`
  (`https://<id>.execute-api.us-east-1.amazonaws.com/<ambiente>`);
- `/api/v1/pagamentos/webhook` é a rota do Billing (F4-20).

Como a notificação chega ao Billing:

1. **Rota pública no gateway (F4-14):** `ANY /api/v1/{proxy+}` usa o authorizer
   de JWT, e o Mercado Pago não manda token do nosso sistema. Por isso existe uma
   rota própria sem JWT em `infra/api-gateway.tf`:

   ```hcl
   route_key          = "POST /api/v1/pagamentos/webhook"
   authorization_type = "NONE"
   ```

   A rota fica aberta, mas não fica insegura: a proteção é o Billing validar o
   `x-signature` com a assinatura secreta e descartar o que não vier do Mercado
   Pago.
2. **ALB (F4-14):** a regra de `/api/v1/pagamentos*` em `infra/alb-servicos.tf`
   entrega a chamada ao Billing.
3. **Billing (F4-20):** valida a assinatura e consulta o pagamento pelo `data.id`
   na API do Mercado Pago antes de marcar o pagamento como confirmado ou recusado.

#### Cuidados no AWS Academy

- **A URL muda a cada recriação do ambiente.** O id do API Gateway é novo a cada
  subida do lab. Depois de cada `sobe-tudo` ou apply do zero, atualize a URL de
  teste no painel. A assinatura secreta não muda, então os secrets do GitHub e do
  Secrets Manager continuam valendo.
- **Uma aplicação avisa um ambiente por vez.** O painel só tem os campos URL de
  teste e URL de produção. Aponte a URL de teste para o ambiente que estiver sendo
  testado. Para dev, hom e prod recebendo ao mesmo tempo, crie uma aplicação por
  ambiente, cada uma com seu par de secrets `_DEV`, `_HOM` e `_PROD`.

### 5. Cadastrar os secrets no GitHub

No repositório `tech-challenge-infra-k8s`, em
*Settings → Secrets and variables → Actions → New repository secret*, crie o par
do ambiente:

```text
MERCADOPAGO_ACCESS_TOKEN_DEV     = <access token do passo 3>
MERCADOPAGO_WEBHOOK_SECRET_DEV   = <assinatura do passo 4>
```

Repita com `_HOM` e `_PROD` para os outros ambientes.

Pela CLI do GitHub, sem o valor passar pelo histórico do terminal:

```bash
gh secret set MERCADOPAGO_ACCESS_TOKEN_DEV --repo tech-challenge-grupo-160/tech-challenge-infra-k8s
gh secret set MERCADOPAGO_WEBHOOK_SECRET_DEV --repo tech-challenge-grupo-160/tech-challenge-infra-k8s
```

O `gh` pede o valor de forma interativa.

### 6. Aplicar

O segredo é gravado pelo apply do `tech-challenge-infra-k8s`, que aplica o
ambiente **inteiro** (rede, EKS, gateway). Por isso não existe um apply "só do
Mercado Pago":

- **Subindo o ambiente (o caso normal):** exporte as duas variáveis e rode o
  `sobe-tudo.sh`. Ele grava o segredo na etapa 3 e, no fim, imprime a URL do
  webhook para o passo 4. Passo a passo em
  [Credenciais do Mercado Pago](https://github.com/tech-challenge-grupo-160/tech-challenge-oficina-mecanica/blob/develop/docs/CICLO-DE-VIDA.md#credenciais-do-mercado-pago).

  ```bash
  read -rsp 'Access token: ' TF_VAR_mercadopago_access_token && echo && export TF_VAR_mercadopago_access_token
  read -rsp 'Webhook secret: ' TF_VAR_mercadopago_webhook_secret && echo && export TF_VAR_mercadopago_webhook_secret
  bash ../tech-challenge-oficina-mecanica/scripts/sobe-tudo.sh --ambiente dev
  ```

- **Pelas pipelines:** basta os secrets do passo 5 estarem cadastrados. O push
  em `develop`, `homolog` e `main` do `infra-k8s` aplica o ambiente da branch com
  eles.

> **Não use o *Terraform CI → Run workflow* do `infra-k8s` só para gravar as
> credenciais** com o ambiente derrubado. Ele sobe o EKS inteiro, falha no meio
> porque as Lambdas não existem e deixa recurso cobrando.
>
> Num apply local do `infra-k8s` sem as duas variáveis exportadas, o Terraform
> **apaga** o valor que já estava no segredo.

### 7. Conferir sem expor o valor

```bash
aws secretsmanager describe-secret --secret-id tc-grupo160/dev/mercadopago \
  --query '{nome: Name, ultimaAlteracao: LastChangedDate, versoes: VersionIdsToStages}'
```

Se aparecer uma versão `AWSCURRENT`, o valor está gravado. Para validar o
conteúdo, confira só as chaves do JSON:

```bash
aws secretsmanager get-secret-value --secret-id tc-grupo160/dev/mercadopago \
  --query SecretString --output text | jq 'keys'
```

O resultado esperado é `["access_token", "webhook_secret"]`.

## Trocar ou revogar uma credencial

1. Gere o novo valor no painel (renovar o access token em **Credenciais** ou
   redefinir a assinatura em **Webhooks**).
2. Atualize o secret do GitHub do ambiente.
3. Rode o Terraform CI do ambiente.
4. Reinicie o Billing:
   `kubectl rollout restart deployment/oficina-mecanica-billing -n oficina-mecanica`.
