# Trin0 Master APP — v1.0

> **Download oficial:** apenas em [github.com/Trin018/Trin0MasterApp/releases](https://github.com/Trin018/Trin0MasterApp/releases).
> O app roda como administrador; não use versões baixadas de outros lugares.
>
> **Contribuições:** este projeto não aceita pull requests. Fique à vontade para fazer um *fork* e modificar a sua própria cópia
> (licença [MIT](LICENSE): mantenha o aviso de copyright).

## Novidades da v1.0
- **Inicialização (FERRAMENTAS):** veja, desative ou remova os programas que abrem junto com o Windows, com opção de desfazer.
- **Instalar apps (FERRAMENTAS):** catálogo ampliado estilo Ninite com 22 aplicações essenciais (Steam, Chrome, Discord, OBS Studio, Voicemeeter, VLC, 7-Zip, etc.) de uma vez depois de formatar, pelo winget.
- **STREAM & OBS (nova categoria):** otimizador para OBS Studio (executar como administrador automático contra rendering lag, forçar GPU dedicada e prioridade alta no processo), módulo para separação de áudio (Voicemeeter Banana e VB-Audio Virtual Cable) e guia com recomendador automático de bitrate/encoder conforme a placa gráfica instalada.
- **Otimização & Trin0 AI Advisor:** diagnóstico inteligente do estado do PC com velocímetro gráfico (0 a 100%), análise de switches, memória RAM, lixo temporário (%TEMP% e cache de shaders), arranque e botão de Auto-Otimizar com IA em 1 clique.
- **Switches Genuínos do Windows:** desbloqueio do plano Desempenho Máximo (Ultimate Performance), prioridade de CPU para jogos (Win32PrioritySeparation), remoção de estrangulamento de rede multimédia (NetworkThrottlingIndex), agendador MMCSS, desativação de hibernação/Fast Startup e desativação de pesquisa web no Menu Iniciar.
- **Personalização Visual e Cores:** seletor dinâmico de cores de destaque no topo (Ciano Elétrico, Roxo Cyber, Verde Neon, Vermelho ROG, Laranja Fogo e Monocromático) com suporte a modo escuro e claro.
- **Abertura mais rápida:** a janela aparece em ~0,5 s (antes ~4,6 s); o hardware é lido em paralelo, em segundo plano.
- **Overlay corrigido:** fica por cima de jogos em janela sem borda como GTA V e Cyberpunk 2077 e segue o monitor do jogo.

Detalhes em [NOVIDADES.md](NOVIDADES.md) (o mesmo texto vai nas notas da release).

App WPF (.NET 8) com:

- **Informações do sistema (categorias + detalhes):** SO, placa-mãe/BIOS, CPU, RAM (por módulo), GPU, discos — via WMI. Em Armazenamento, cartões por unidade (C:, pendrive, HD externo, rede) com anel de cheio/vazio, tipo e disco físico (`DriveMap.cs`).
- **Avisos de dispositivo (`ToastWindow.xaml`):** ao conectar/remover pendrive, HD externo ou unidades USB, aparece um aviso no canto da tela e a lista de unidades se atualiza (via `WM_DEVICECHANGE`, sem ficar consultando).
- **Sensores em tempo real:** temperatura, clock, carga, voltagem, ventoinhas, consumo etc. — via LibreHardwareMonitorLib. Atualiza a cada 1 s, com filtro por tipo.
- **Placa de vídeo (estilo GPU-Z):** chip, arquitetura, processo, die, transistores, shaders/SMs/Tensor/RT, ROPs/TMUs, fillrates, TFLOPS, memória (tamanho, tipo, barramento, clock, banda, Resizable BAR), clocks, PCIe (máximo e em uso), limites de energia, VBIOS, driver, assinatura WHQL, CUDA, DirectX (feature level), APIs e recursos da arquitetura. Sensores ao vivo da GPU + P-state, PerfCap, NVENC/NVDEC. Fontes: NVML (NVIDIA), DXGI/Direct3D 12, WMI/PnP, registro e banco de specs em `GpuSpecDb.cs`.
- **Stress test:** carga de CPU em todos os threads (FPU + inteiros) e/ou RAM (grava e verifica padrões em 40–80% da memória livre). Para sozinho quando a temperatura da CPU atinge o limite configurado, quando o tempo acaba ou se aparecer erro de memória. Mostra temperatura atual/máxima, carga, consumo, gráfico dos últimos 5 min e um log dos eventos.
- **Stress de GPU (inspirado no FurMark, `GpuBurn.cs`):** torus com pelos renderizado por *shells* em Direct3D 11 (Vortice), com overdraw massivo, cadeia de FMAs no pixel shader e alpha blending, sem limite de FPS, numa janela própria. Opções: resolução (720p/1080p/1440p/tela cheia), MSAA (off/4x/8x), intensidade (Normal/Alta/Extrema) e limite de temperatura da GPU. Mostra FPS, temperatura, consumo e PerfCap; ao final registra frames e FPS médio. ESC ou fechar a janela encerra o teste. Medido numa RTX 5070 (1080p, 4x MSAA, Alta): ~99% de uso e 220–232 W de 250 W.
- **Jogos:** FPS esperado em 32 jogos populares para a sua GPU + CPU, por resolução (1080p/1440p/4K), gráficos e upscaling (DLSS/FSR). Com chave da [HowManyFPS API](https://howmanyfps.com/developer-api) (pedida em info@howmanyfps.com) mostra FPS médio e 1% low medidos em sessões reais; sem chave, usa uma estimativa local aproximada (`GameCatalog.cs`). A chave fica cifrada com DPAPI em `%AppData%\ResetExplode` e as respostas em cache por 7 dias.
- **Detector de gargalo:** equilíbrio teórico GPU × CPU; leitura ao vivo (carga da GPU, CPU total, núcleo mais ocupado, VRAM, RAM, temperaturas, limitador térmico) com veredito; e análise de sessão (1/3/5 min) com % do tempo limitado por GPU, CPU, VRAM, RAM, temperatura ou trava de FPS, mais recomendações (`BottleneckAnalyzer.cs`).
- **Overlay de jogos (estilo RTSS/Afterburner, `OverlayWindow.xaml`):** FPS, frametime, 1% low, gráfico de frametime, GPU/CPU/RAM/VRAM. Janela transparente, sempre no topo e que ignora cliques; atalho global **Ctrl+Shift+O**. Itens escolhidos pelo usuário (`OverlayMetrics.cs`, `MainWindow.Overlay.cs`): FPS/frametime/1% low/gráfico, GPU (uso, temperatura, hotspot, clocks do núcleo e da memória, consumo, tensão, ventoinha, PerfCap), VRAM, CPU (uso, núcleo mais ocupado, temperatura, clock médio e máximo, consumo), RAM, nome do jogo e hora. Volta para o topo a cada troca de janela ativa (jogos em janela sem borda também se marcam como "sempre no topo") e fica no monitor do jogo. Não aparece em tela cheia exclusiva. O FPS de qualquer jogo é medido via ETW (eventos Present de DXGI/D3D9, como o PresentMon, `FrameMonitor.cs`), sem injetar nada no jogo.
- **BENCHMARK (`BenchRenderer.cs`):** benchmark de CPU no princípio do Cinebench, com renderizador próprio. Path tracer monocromático (iluminação global, sombras suaves, metal e vidro com refração), 960×540 a 1024 amostras/pixel, imagem em blocos de 32 px distribuídos entre threads do centro para fora. Multi-core (todas as threads) e single-core (1 thread, imagem inteira, ~1min40s num 7700X), 1 passada ou mínimo de 10 min. Nota = amostras/s ÷ 10.000, determinística (sementes fixas por pixel). Histórico dos últimos 10 resultados. Referência: Ryzen 7 7700X ≈ 4.500 multi / 530 single.
- **BENCHMARK · benchmarks de jogo:**
  - *Cena de jogo (GPU)* (`GpuGameBench.cs`): vale em Direct3D 11 com terreno fractal, 40 mil árvores por instancing, sombras 4096² com PCF, água, neblina e pós HDR; câmera em trajeto fixo por tempo, 1080p/1440p/4K, 30/60 s. Nota = FPS médio × 10. RTX 5070 em 1080p ≈ 680 FPS. Os sensores ficam pausados durante o teste (consultas ao driver geravam engasgos no 1% low).
  - *Ray tracing (GPU)* (`GpuRtBench.cs`, `MainWindow.Rt.cs`): Direct3D 12 com **DXR 1.1** (raios inline num compute shader), usa os núcleos de RT (RTX 20+, Radeon RX 6000+, Intel Arc). 4 mil objetos (fosco, metal, espelho, luzes), path tracing com sombra suave, oclusão de ambiente, reflexos e iluminação indireta; TLAS refeita a cada frame. Presets Normal/Alta/Extrema (2/4/8 amostras, 2/3/4 rebatidas). Confiabilidade: aquecimento sem medir + 3 passadas; nota pela mediana (FPS × 10), variação entre passadas (confiável abaixo de 3%), tempo de frame pelos timestamps da GPU. Referência: RX 9070, Alta 1440p, modo Padrão = 878 pts (87,8 FPS), variação 0,5%.
  - *Simulação de jogo (CPU)* (`GameSimBench.cs`): 30 mil unidades em frente de batalha; thread principal (grade, lógica, preparo do desenho) + workers (física, colisão, IA). Nota = FPS da simulação. Ryzen 7 7700X ≈ 290 FPS.
  - *Memória e carregamento* (`MemoryBench.cs`): latência e banda da RAM; carregamento de mapa (pacote de 512 MB + mil arquivos de 64 KB) lido sem cache do Windows, com passada prévia para o antivírus e 8 threads. Arquivos temporários apagados no final.
  - *Disco* (`DiskBench.cs`): leitura e escrita sequencial (1 MB) e aleatória 4K (QD1 e QD32), sem cache do Windows (`FILE_FLAG_NO_BUFFERING`), em arquivo temporário de 512 MB / 1 GB / 4 GB na unidade escolhida; apagado no final. Histórico dos últimos resultados.
- **OVERCLOCK (`MainWindow.Oc.cs`, `OcCheck.cs`):** ajuste de limite de energia e travamento de clock da GPU NVIDIA (via NVML) com botão de restaurar, e verificação de estabilidade de overclock (CPU, GPU ou ambos; 5/15/30 min) com limites de segurança de temperatura, leitura de erros de hardware (WHEA, reset de driver de vídeo), veredito, histórico e guia de BIOS conforme a placa-mãe e a CPU.
- **OTIMIZAÇÃO (`Optimizer.cs`, `WindowsTweaks.cs`, `PcAdvisor.cs`):** painel com velocímetro de otimização heurístico por IA (0–100%), botão de Auto-Otimização em 1 clique, limpeza da memória em espera (como o ISLC), limpeza de ficheiros temporários e cache de shaders (%TEMP%, DirectX Cache), e encerramento de programas marcados. Switches genuínos do Windows com interruptor e "Restaurar originais": plano Desempenho Máximo, plano Alto desempenho, prioridade de CPU para jogos (Win32PrioritySeparation), remoção de estrangulamento de rede multimédia (NetworkThrottlingIndex), prioridade MMCSS, Modo de Jogo, desativar Game DVR, HAGS, otimizações para jogos em janela, aceleração do mouse, desativar hibernação e pesquisa web no Menu Iniciar. Nada que reduza a segurança (Defender, VBS, UAC, Update).
- **STREAM & OBS (`MainWindow.Stream.cs`):** central para streamers e criadores. Configuração de modo Administrador no OBS Studio contra congelamentos quando o jogo usa 99% da GPU, forçar GPU dedicada para captura sem ecrã preto, botão de prioridade Alta no processo, instalador/gestor do Voicemeeter Banana e VB-Audio Virtual Cable para separação do áudio de Jogo, Discord e Música na live, e tabela de configurações recomendadas de encoder (NVENC, AMD, x264) para Twitch e YouTube.
- **Inicialização (`StartupManager.cs`, `MainWindow.Startup.cs`):** lista os programas que abrem com o Windows (chaves Run do usuário e de todos os usuários, 32 e 64 bits, e pastas Inicializar) com descrição e fabricante. Interruptor para ativar/desativar igual ao Gerenciador de Tarefas (chaves StartupApproved) e botão REMOVER, com cópia em `%AppData%\ResetExplode` para "Restaurar removidos".
- **Instalar apps (`AppInstaller.cs`, `MainWindow.Apps.cs`):** instala os essenciais pós-formatação organizados por categorias estilo Ninite (Navegadores, Jogos & Launchers, Comunicação, Streaming & Áudio, Utilitários & Hardware) pelo winget, em silêncio e um de cada vez, mostrando o resultado de cada um. Detecta o que já está instalado (Aplicativos instalados do Windows).
- **Saúde dos discos (S.M.A.R.T.) (`DiskHealth.cs`, `MainWindow.Disks.cs`):** página própria com um cartão por disco físico — veredito (Bom/Atenção/Ruim) com os motivos, temperatura, horas ligado, ciclos, desgaste do SSD, setores realocados, erros de leitura/escrita e previsão de falha. Fontes: Windows Storage (MSFT_PhysicalDisk, MSFT_StorageReliabilityCounter) e MSStorageDriver_FailurePredictStatus.
- **Relatório exportável (`ReportBuilder.cs`):** botão EXPORTAR RELATÓRIO em Sistema. Gera um arquivo HTML ou JSON com as informações do sistema, RAM por módulo, discos e saúde dos discos.
- **RAM detalhada:** resumo (capacidade, canais, tipo DDR4/DDR5/LPDDR5, velocidade) e, por módulo, fabricante, part number, formato, voltagem, clock configurado e localização do slot.
- **Energia (`BatteryInfo.cs`, `MainWindow.Net.cs`):** bateria de notebook com desgaste (capacidade atual × de projeto), ciclos de carga, estado, autonomia, tensão e potência, com veredito; fonte de energia (tomada/bateria), plano de energia do Windows e consumo dos sensores de CPU e GPU. Em PC de mesa mostra só o cartão de energia.
- **Rede (`NetworkTest.cs`):** diagnóstico de lag em ~30 s — latência, jitter e perda de pacotes até o roteador e até a internet (1.1.1.1 e 8.8.8.8), tempo de DNS, velocidade de download/upload (servidor público speed.cloudflare.com) e latência sob carga (bufferbloat). O veredito aponta se o problema está na rede local (Wi-Fi/cabo/roteador), no provedor ou se a conexão está estável.
- **INTEGRAÇÃO RGB (`LightBackends.cs`, `CorsairSdk.cs`, `CorsairSdkInstaller.cs`, `MainWindow.Integ.cs`): controle de iluminação e efeitos de todo o PC numa tela só (em teste). Pontes: Corsair iCUE / iCUE LINK (iCUE SDK v4; a DLL é baixada pelo programa, com confirmação e verificação de assinatura), OpenRGB (servidor SDK local; é o caminho para MSI Mystic Light, Gigabyte RGB Fusion, ASUS Aura, RAM, Razer, Logitech e outras, já que essas marcas não têm SDK público) e Razer Chroma (API REST do Synapse). Efeitos: cor fixa, respiração, arco-íris em onda, e cores que seguem a temperatura da CPU/GPU ou a carga da CPU, com brilho e velocidade. Ao devolver ou fechar, cada fabricante reassume as luzes. Para MSI/Gigabyte/ASUS: abra o OpenRGB com o servidor ligado e feche MSI Center, Armoury Crate e RGB Fusion.
- **BIBLIOTECA DE JOGOS (`GameLibrary.cs`, `GameLauncher.cs`, `Vdf.cs`, `MainWindow.Launcher.cs`): inicializador que detecta os jogos instalados (Steam, Epic Games, GOG, Ubisoft Connect e .exe adicionados à mão) e guarda um perfil por jogo: prioridade do processo, plano de energia Alto desempenho só durante o jogo, GPU dedicada e sem otimizações de tela cheia (por .exe, no registro do usuário), menu de gráficos (modo de janela, resolução, taxa de atualização e extras convertidos em argumentos de inicialização no estilo Source/CS2, Unity ou Unreal) e programas auxiliares (ferramentas de terceiros que abrem antes do jogo e fecham junto). Em jogos da Steam, um botão grava os argumentos nas opções de inicialização (localconfig.vdf, com backup, fechando a Steam antes). O botão JOGAR aplica o perfil, inicia e desfaz o que for temporário quando o jogo fecha.

## Interface
- Visual em blocos com cartões arredondados, inspirado em painéis de dashboard modernos. Um único menu lateral com Visão geral no topo e categorias recolhíveis (começam fechadas; clique para ver as sub-abas):
  - *Visão geral* (`MainWindow.Dash.cs`): gráfico de área CPU × GPU (temperatura, carga ou consumo; 1/5/15 min), desempenho do BENCHMARK, tabela de componentes, anéis de RAM/VRAM/disco, eventos recentes e saúde do PC.
  - **SISTEMA** · Sistema, Sensores, Placa de vídeo, Discos, Energia, Rede.
  - **JOGOS** · Biblioteca, FPS estimado, Gargalo, Overlay.
  - **STREAM & OBS** · OBS & Áudio (Voicemeeter Banana e encoders).
  - **DESEMPENHO** · Benchmark, Stress test, Overclock.
  - **FERRAMENTAS** · Otimização, Inicialização, Instalar apps, Drivers, Integração RGB.
- Seletor de cores de destaque no topo (Ciano, Roxo, Verde, Vermelho, Laranja, Monocromático) e botão de alternar modo escuro/claro (`ThemeManager.cs`). O overlay de jogos e os avisos de dispositivo ficam sempre escuros.
- Leitura ao vivo de temperatura e carga da CPU e da GPU sempre visível no rodapé do menu.
- Sensores com busca e filtros por tipo (chips); tema em `Theme.xaml`.

## Base utilizada
- Interface, stress test e gráficos: código próprio.
- Sensores: [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL 2.0) — mantenha o aviso de licença ao distribuir.
- Informações do sistema: WMI do Windows.
- Medição de FPS: [TraceEvent](https://github.com/microsoft/perfview) (MIT), da Microsoft, para ler eventos ETW.
- Renderização do stress de GPU: [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) (MIT), bindings de Direct3D 11 para .NET. Direct3D 12 e DXC (compilador de shaders da Microsoft) também via Vortice, no teste de ray tracing.

## Requisitos
- Windows 10/11
- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Driver **PawnIO** (temperatura/consumo da CPU): `winget install namazso.PawnIO`
  A LibreHardwareMonitorLib 0.9.6+ usa o PawnIO no lugar do antigo WinRing0, que o Windows Defender bloqueia.

## Como rodar
```powershell
cd Trin0MasterApp
dotnet run
O app pede privilégio de administrador (necessário para sensores de baixo nível e ajustes do Windows). Para levar a outro PC: dê dois cliques em publicar.bat. Ele gera ..\Trin0MasterApp-dist\Trin0MasterApp.exe (arquivo único, com o .NET embutido, sem caminhos do seu usuário) + LEIA-ME.txt. No outro PC só é preciso instalar o PawnIO. Rode de novo após cada alteração no código.
Baixar a versão mais recente
A cada push na main, o GitHub Actions (.github/workflows/publicar.yml) gera o Trin0MasterApp.exe e atualiza a release latest (aba Releases do repositório), com o .exe e um .zip. É só baixar de lá, sem compilar. Ao criar uma tag (git tag v1.0 e git push --tags), também sai uma release fixa dessa versão.
Observações
Se algum sensor não aparecer, depende do suporte da biblioteca à sua placa-mãe.
Já feito
✔ Informações do sistema, sensores, placa de vídeo, jogos, gargalo, overlay
✔ Stress test com limite de temperatura (CPU, RAM, GPU)
✔ Benchmarks: CPU, GPU, simulação, memória e disco
✔ Saúde dos discos (S.M.A.R.T.)
✔ Relatório exportável (HTML/JSON)
✔ OVERCLOCK (limite de energia, clock e verificação de estabilidade)
✔ Energia (bateria e fonte) e Rede (latência, perda e velocidade)
✔ INTEGRAÇÃO RGB: iluminação Corsair iCUE, OpenRGB (MSI/Gigabyte/ASUS) e Razer Chroma
✔ DRIVERS: inventário de drivers, NVIDIA (verifica a versão, baixa, confere a assinatura e abre o instalador), AMD/ASUS/MSI/Gigabyte pela página oficial, Windows Update e backup (DriverManager.cs)
✔ BIBLIOTECA DE JOGOS: inicializador com detecção de jogos, perfil por jogo e opções de inicialização da Steam
✔ Inicialização do Windows: ativar, desativar e remover programas, com desfazer
✔ Abertura rápida: janela na hora, hardware carregado em paralelo
✔ Instalar apps essenciais pós-formatação pelo winget (catálogo expandido de 22 apps estilo Ninite)
✔ STREAM & OBS: otimizador do OBS, guia de separação de áudio com Voicemeeter Banana e recomendador de encoder
✔ Otimização com Trin0 AI Advisor: velocímetro de estado de 0 a 100%, limpeza de RAM, limpeza de cache/temporários e switches genuínos do Windows
✔ Seletor dinâmico de cores de destaque no topo (Ciano, Roxo, Verde, Vermelho, Laranja, Monocromático)
Próximas ideias
Relatório em CSV e com gráficos
Comparar resultados de benchmark entre PCs / histórico de relatórios
Leitura direta de SPD / Super I/O
INTEGRAÇÃO RGB: perfis salvos, efeitos por jogo, SDKs nativos da MSI/ASUS/Gigabyte se forem liberados
Alertas de temperatura, bateria e saúde de disco em segundo plano
Versão com instalador e assinatura digital
Licença
MIT — veja o arquivo LICENSE.
code
Code
