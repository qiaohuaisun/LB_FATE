# LB_FATE Avalonia GUI Client

A modern cross-platform GUI client for LB_FATE game built with Avalonia UI.

## Features

### ✨ Current Features

- **Modern GUI Interface**: Clean, dark-themed interface with tabbed layout
- **Real-time Game Board**: Visual 10x10 grid with colored unit representations
- **Interactive Controls**:
  - Left-click empty cells to move
  - Right-click enemy units to attack
  - Command input box for advanced commands
- **Unit Status Display**: Real-time HP/MP bars for all units
- **Skills Panel**: List available skills with cooldown tracking
- **Combat Log**: Scrollable log of recent game events
- **Connection Management**: Easy server connection interface
- **Cross-platform**: Runs on Windows, Linux, and macOS

### 🎮 Controls

#### Mouse Controls
- **Left-click** on empty cell → Move to that position
- **Right-click** on enemy unit → Attack that unit

#### Command Box
Type commands directly:
- `move x y` - Move to position (x, y)
- `attack P#` - Attack player with ID P#
- `use n P#` - Use skill #n on target P#
- `use n x y` - Use skill #n on position (x, y)
- `use n up/down/left/right` - Use skill in direction
- `pass` - End your turn
- `skills` - List available skills
- `info` - Show role information
- `help` - Show help

#### Quick Buttons
- **Send** - Execute command in text box
- **Pass** - End your turn immediately
- **Skills** - Refresh skills list
- **Help** - Show quick help

## Building and Running

### Prerequisites
- .NET 8.0 SDK
- ETBBS core library (automatically referenced)

### Build
```bash
dotnet build LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj -c Release
```

### Run
```bash
dotnet run --project LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj
```

Or on Windows:
```cmd
LB_FATE.AvaloniaClient\bin\Release\net8.0\LB_FATE.AvaloniaClient.exe
```

## Usage

### 1. Start the Server

First, start the LB_FATE server using one of the launcher scripts:

**Windows**:
```cmd
cd publish
runServer.cmd
```

Select your desired log level and configure:
- Number of players (e.g., 2)
- Game mode (ffa or boss)
- Port (default: 35500)

### 2. Launch the Client

Run the Avalonia client application.

### 3. Connect to Server

1. On the **Connection** tab, enter:
   - Server Host: `127.0.0.1` (for local server)
   - Server Port: `35500` (or your custom port)
2. Click **Connect to Server**
3. Wait for "Connected" status

### 4. Play the Game

1. Switch to the **Game** tab (automatically enabled when connected)
2. Wait for your turn (green "YOUR TURN" indicator appears)
3. View the game board:
   - Different colors represent different classes
   - HP bars show unit health
   - Numbers show unit IDs
4. Take actions:
   - Click cells to move
   - Right-click enemies to attack
   - Use skills via buttons or commands
5. End turn with **Pass** button or `pass` command

## Architecture

### Project Structure

```
LB_FATE.AvaloniaClient/
├── Models/
│   └── GameState.cs           - Client-side game state
├── ViewModels/
│   ├── ViewModelBase.cs       - Base ViewModel
│   ├── MainWindowViewModel.cs - Main window ViewModel
│   └── GameViewModel.cs       - Game logic ViewModel
├── Views/
│   ├── MainWindow.axaml       - Main window layout
│   ├── MainWindow.axaml.cs
│   ├── GameView.axaml         - Game interface layout
│   └── GameView.axaml.cs
├── Controls/
│   ├── GameBoardControl.axaml - Custom game board control
│   └── GameBoardControl.axaml.cs
├── Services/
│   ├── GameClient.cs          - TCP client for server communication
│   └── GameStateParser.cs     - Parse server messages
└── Assets/                     - Icons and resources
```

### MVVM Pattern

This client follows the **Model-View-ViewModel (MVVM)** pattern:

- **Models**: Data structures (`GameState`, `UnitInfo`, `SkillInfo`)
- **ViewModels**: Business logic and state management (`GameViewModel`)
- **Views**: XAML UI definitions (`MainWindow`, `GameView`)

### Key Components

#### GameClient
Handles TCP connection to the server:
- Async connect/disconnect
- Send commands
- Receive messages in background thread
- Event-driven architecture

#### GameStateParser
Parses server text messages into structured data:
- Board state (Day/Phase)
- Unit information (HP/MP/Position)
- Skill details
- Combat logs

#### GameBoardControl
Custom Avalonia control for rendering the game board:
- Canvas-based drawing
- Grid lines and coordinates
- Unit visualization with class colors
- HP bars
- Mouse interaction

#### GameViewModel
Central ViewModel managing:
- Connection state
- Game state updates
- User commands
- Observable collections for UI binding

## Color Scheme

### Class Colors
- **Saber**: Cyan (#00CED1)
- **Archer**: Green (#32CD32)
- **Lancer**: Blue (#4169E1)
- **Rider**: Gold (#FFD700)
- **Caster**: Magenta (#FF00FF)
- **Assassin**: Dark Cyan (#008B8B)
- **Berserker**: Red (#DC143C)

### UI Colors
- Background: Dark (#1e1e1e, #2b2b2b)
- Grid Lines: Gray (#444444)
- HP Bar: Green (#00FF00) / Dark Red (#8B0000)
- Active Turn: Lime Green
- Logs: Light Gray

## Network Protocol

The client communicates with the server using text-based TCP protocol:

### Client → Server
- Command strings (e.g., "move 5 3", "attack P1", "pass")

### Server → Client
- Board state text (parsed by `GameStateParser`)
- Unit status lines
- Combat log messages
- "PROMPT" when it's player's turn
- "GAME OVER" when game ends

## Future Enhancements

### Planned Features
- [ ] Animations for movements and attacks
- [ ] Skill range preview
- [ ] Sound effects and background music
- [ ] Battle replay system
- [ ] Multiple server profiles
- [ ] Chat system
- [ ] Statistics dashboard
- [ ] Settings/preferences panel
- [ ] Custom themes
- [ ] Mobile-friendly touch controls

### Technical Improvements
- [ ] Better error handling and reconnection
- [ ] Message queue for smoother updates
- [ ] Unit sprite/avatar system
- [ ] Particle effects for skills
- [ ] Minimap for larger boards
- [ ] Performance optimizations
- [ ] Localization support (i18n)

## Dependencies

- **Avalonia**: 11.3.6 - Cross-platform UI framework
- **CommunityToolkit.Mvvm**: 8.2.1 - MVVM helpers
- **ETBBS**: Core game library (project reference)

## Development

### Adding New Views

1. Create XAML file in `Views/`
2. Create corresponding ViewModel in `ViewModels/`
3. Bind DataContext in XAML or code-behind

### Extending GameClient

The `GameClient` class is extensible via events:
```csharp
client.MessageReceived += (sender, msg) => { /* Handle message */ };
client.ConnectionStatusChanged += (sender, connected) => { /* Handle status */ };
client.ErrorOccurred += (sender, error) => { /* Handle error */ };
```

### Custom Controls

Add custom controls to `Controls/` directory and reference them in views:
```xaml
xmlns:controls="using:LB_FATE.AvaloniaClient.Controls"
<controls:YourControl />
```

## Troubleshooting

### Connection Failed
- Ensure server is running
- Check firewall settings
- Verify correct IP/port
- Check server logs for errors

### Board Not Updating
- Check connection status
- Ensure server is sending board updates
- Look for parsing errors in logs

### UI Not Responding
- Check for exceptions in console
- Restart the client
- Rebuild the project

## License

Same as parent project (ETBBS).

## Contributing

1. Fork the repository
2. Create feature branch
3. Commit changes
4. Push to branch
5. Create Pull Request

---

**Built with ❤️ using Avalonia UI and .NET 8**