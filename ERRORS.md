# Erros tratados

Consultar este arquivo antes de investigar um problema relacionado.

## Pedido de bateria sem resposta

- **Sintoma:** o pedido era escrito na interface `MI_02`, mas nenhuma resposta chegava em 1,5 s. Pouco depois o dongle trocou de `PID_1098` para `PID_2106`.
- **Causa:** o controle estava desligado. O `PID_2106` é o estado normal do dongle sem controle, e a troca não é instantânea.
- **Solução:** tratar a falta de resposta como "sem leitura". Ocultar o widget após 2 falhas seguidas e nunca mostrar um valor antigo como atual.
- **Prevenção:** o `PID_1098` sozinho não garante leitura. Um percentual só aparece depois de uma resposta válida.

## Duplo clique no widget não abria as configurações (0.1.0)

- **Sintoma:** com cliques reais do mouse, o duplo clique não fazia nada. O arraste funcionava, mas deslocava o widget mais do que o mouse (23 px a mais na vertical em um movimento de 40 px).
- **Causa:** `DragMove` entra no loop modal do sistema já no primeiro clique e consome o segundo, então `ClickCount` nunca chegava a 2.
- **Solução:** arraste próprio com `CaptureMouse`, que só começa depois da distância mínima de arraste do sistema.
- **Prevenção:** testar interações de mouse com cliques reais. Automação de interface e imagens geradas não passam por esse caminho.

## `--snapshot` apagava as preferências do usuário (0.1.1)

- **Sintoma:** tamanho e opacidade escolhidos voltavam a 64 e 50% depois de gerar imagens.
- **Causa:** o modo `--snapshot` não carrega as preferências, mas os sliders da tela de configurações disparavam o salvamento dos valores padrão.
- **Solução:** o modo `--snapshot` nunca grava preferências.
- **Prevenção:** ferramentas de diagnóstico não devem tocar nos dados do usuário.

## Preferências e início com o Windows só na visão do Claude (0.5.0)

- **Sintoma:** o app aberto fora da sessão voltou a 64 px e 50%, e a chave `Run` real não tinha o PadTide, embora os dois parecessem certos na sessão do agente.
- **Causa:** o Claude desktop é um pacote MSIX. Processos iniciados a partir do shell do agente veem `%APPDATA%`, `%LOCALAPPDATA%` e parte de `HKCU` virtualizados. O app aberto assim gravou preferências e `Run` só em `Packages\Claude_*\LocalCache`.
- **Solução:** preferências restauradas no arquivo real, com backup, e app reaberto por `Win32_Process.Create`, que não herda o pacote.
- **Prevenção:** abrir e conferir o app instalado com `verify-installed.ps1`. Ler dados reais por `\\localhost\C$\Users\<usuário>\AppData\...` e a `Run` real por um processo criado via WMI. Nunca concluir algo sobre estado real a partir do shell do agente.

## Barras e botões com formato de lente

- **Sintoma:** os trilhos dos sliders saíam com as pontas afinadas, como uma lente.
- **Causa:** no WPF, um `CornerRadius` muito maior que o elemento (por exemplo `999`) é reduzido mantendo a proporção entre os eixos, o que gera curvas elípticas.
- **Solução:** usar um raio igual à metade da altura do elemento, nos tokens `Radius.*`.

## Handle nulo no callback da DLL da GameSir

- **Sintoma:** ao investigar o protocolo, o callback de dispositivo da `sapjoyuniapis_t4n2` entregava `dev=0` com status conectado.
- **Causa:** essa DLL atende um dispositivo só e aceita o ponteiro nulo como válido.
- **Solução:** o PadTide não usa essa DLL. O pedido HID foi registrado uma vez e é enviado diretamente.
