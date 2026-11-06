# Erros tratados

Consultar este arquivo antes de investigar um problema relacionado.

## Pedido de bateria sem resposta

- **Sintoma:** o pedido era escrito na interface `MI_02`, mas nenhuma resposta chegava em 1,5 s. Pouco depois o dongle trocou de `PID_1098` para `PID_2106`.
- **Causa:** o controle estava desligado. O `PID_2106` é o estado normal do dongle sem controle, e a troca não é instantânea.
- **Solução:** tratar a falta de resposta como "sem leitura". Ocultar o widget após 2 falhas seguidas e nunca mostrar um valor antigo como atual.
- **Prevenção:** o `PID_1098` sozinho não garante leitura. Um percentual só aparece depois de uma resposta válida.

## Barras e botões com formato de lente

- **Sintoma:** os trilhos dos sliders saíam com as pontas afinadas, como uma lente.
- **Causa:** no WPF, um `CornerRadius` muito maior que o elemento (por exemplo `999`) é reduzido mantendo a proporção entre os eixos, o que gera curvas elípticas.
- **Solução:** usar um raio igual à metade da altura do elemento, nos tokens `Radius.*`.

## Handle nulo no callback da DLL da GameSir

- **Sintoma:** ao investigar o protocolo, o callback de dispositivo da `sapjoyuniapis_t4n2` entregava `dev=0` com status conectado.
- **Causa:** essa DLL atende um dispositivo só e aceita o ponteiro nulo como válido.
- **Solução:** o PadTide não usa essa DLL. O pedido HID foi registrado uma vez e é enviado diretamente.
