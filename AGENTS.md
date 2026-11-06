# Orientações para agentes

## Produto

PadTide é um widget de bateria para o GameSir Nova 2 Lite, em C#/WPF sobre .NET Framework 4.8, sem dependências além do que o Windows já traz. Leveza faz parte do produto: com o controle desligado nada pode fazer polling, e nenhum runtime, serviço, telemetria ou pacote residente entra sem necessidade aprovada.

- Código, comentários e mensagens de commit em inglês; textos da interface, README e changelog em português do Brasil.
- Toda a interface usa os componentes e tokens de `src/PadTide/Theme`. Sem controles nativos, cores ou medidas soltas, e sem bordas sólidas: superfícies se separam por tom.
- Mudanças visuais: conferir com `PadTide.exe --snapshot <pasta>`.

## Protocolo do controle

A única mensagem enviada ao controle é o pedido de `BatteryReport.CreateRequest`. Não sondar outros comandos nessa interface: `[0,242,0,4,246]`, por exemplo, coloca o dongle em modo de atualização de firmware. `PID_1098` significa controle ligado pelo dongle; `PID_2106`, só o dongle; `PID_100F`, controle no cabo, reconhecido só pela presença, sem nenhum pedido.

## Módulo do SwissTechKnife

`src/PadTide.Module` é um supervisor fino que roda o mesmo app como `PadTide.Hosted.exe --hosted --parent <pid> --data <pasta>`. No modo hospedado, o host é dono da bandeja, das atualizações, do início com o Windows e de encerrar o app; esses controles ficam ocultos. A página do módulo usa só chaves do SDK. O módulo e o app se falam pela janela `PadTide.Host` e pelo mutex de instância única: mudar um deles exige mudar o outro.

## Versionamento

- `VERSION` é a fonte única da versão, SemVer sem `v`. Não duplicar em projetos ou no instalador.
- Toda alteração entregue faz o bump: `MAJOR` para quebra, `MINOR` para recurso compatível, `PATCH` para correção.
- Na mesma alteração, registrar a versão em `CHANGELOG.md` (Keep a Changelog), com notas breves e voltadas ao usuário.
- O push em `main` que altera `VERSION` ou `CHANGELOG.md` publica `vX.Y.Z` com `PadTide-Setup-X.Y.Z.exe` e `padtide-X.Y.Z.zip`. A versão e as `releaseNotes` em inglês de `src/PadTide.Module/module.json` acompanham o `VERSION`; a sincronização do catálogo do SwissTechKnife publica o módulo sozinha. A seção do changelog precisa corresponder à versão. Versões publicadas não são sobrescritas, e o workflow mantém só as 3 mais recentes.

## Validação e entrega

- Consultar `ERRORS.md` antes de investigar erro relevante e registrar erros não triviais resolvidos.
- Rodar `scripts/build.ps1`, que compila, gera o ícone e executa os testes.
- O resumo final informa `Versão esperada: X.Y.Z` e o que foi ou não testado com o controle real.
- Não declarar release concluída sem conferir o workflow, a tag e os arquivos no GitHub.
- A chave `Run` do registro é compartilhada: alterar só o valor `PadTide`.
- Processos iniciados pelo shell do agente veem pastas e parte do registro virtualizados pelo pacote do Claude. Provar o app instalado por um processo criado via WMI e ler o estado real por `\\localhost\C$` (ver `ERRORS.md`).
