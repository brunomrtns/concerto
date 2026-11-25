# CI_QA — Testes Automáticos a Cada Push (GitHub Actions)

Configuração de Integração Contínua (CI) de CONCERTO executando automaticamente a suíte **EditMode + PlayMode** (`Tests/SmokeTest/` e demais testes) em batchmode a cada `push` ou `pull_request` em `main`.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Segurança de Credenciais:** 100% das credenciais de licença Unity (`UNITY_LICENSE` / `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`) ficam **exclusivamente em GitHub Secrets** — nenhuma chave ou senha existe no código.
- **Escopo fechado:** Apenas infraestrutura de CI (`.github/workflows/tests.yml` e `CI_QA.md`), sem alterar nenhuma linha de gameplay.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.
- **Suíte local `Tests/SmokeTest/`:** **18/18 PASSED**, `0 warnings CS`, `0 errors CS`.

---

## 2. Pré-requisito de Licença Unity (GitHub Secrets)

No repositório `brunomrtns/concerto`, acesse **Settings → Secrets and variables → Actions → New repository secret** e configure:
- **`UNITY_LICENSE`**: conteúdo completo do arquivo `.ulf` (Personal) **OU** **`UNITY_SERIAL`** (Pro/Plus)
- **`UNITY_EMAIL`**: e-mail da conta Unity
- **`UNITY_PASSWORD`**: senha da conta Unity

*(Alternativa: registrar um **self-hosted Windows runner** na máquina local com Unity `6000.3.26f1` já ativado, evitando exportação de `.ulf` e reduzindo o tempo para ~60s.)*

---

## 3. Tempo Estimado de Execução

- **Primeira execução (cold cache `Library/`):** `~12–18 minutos` em `ubuntu-latest`.
- **Execuções seguintes (com `actions/cache@v4` em `unity/Library`):** `~6–10 minutos`.
- **Self-hosted Windows local (opcional):** `~1–2 minutos`.

---

## 4. Como Ler um Run Falho (Passo a Passo)

1. Abra a aba **Actions** do repositório `brunomrtns/concerto` e clique no run marcado com **❌ vermelho**.
2. Clique no job **`Unity EditMode + PlayMode Tests`**.
3. Expanda o step que falhou:
   - Se falhou em **`Verify Unity License Secrets`**: faltam os secrets da seção 2.
   - Se falhou em **`Run Unity EditMode & PlayMode Tests`**: procure as linhas `[FAIL]` / `AssertionException` / `error CS`.
4. Role até o final da página do run em **Artifacts** e baixe **`unity-test-results-<run_number>`**:
   - Abra **`test-report.html`** no navegador para ver a tabela resumida (verde/vermelho) de cada teste.
   - Abra os arquivos **`.xml`** (NUnit) para o stack trace completo do teste falho.

---

## 5. O Que Fazer Quando o Check Fica Vermelho

1. **Não faça merge nem continue empilhando commits** sobre `main` vermelho.
2. Reproduza o teste falho localmente em batchmode:
   ```powershell
   Unity.exe -batchmode -projectPath C:\Users\Bruno\workspace\game-concerto\unity `
     -runTests -testPlatform PlayMode -testFilter Studio.Tests.SmokeTest.ConcertoSmokeTests
   ```
3. Corrija a causa raiz (ou reverta o commit causador com `git revert <hash>`).
4. Confirme **100% VERDE** localmente (`0 warnings CS`, `0 errors CS`), faça commit + push e aguarde o check ficar **✅ verde** no GitHub Actions.
