import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: vscode.ExtensionContext) {
    console.log('LBR Language Server extension is now active');

    const config = vscode.workspace.getConfiguration('lbrLanguageServer');

    // Get server path from configuration or use bundled server
    let serverPath = config.get<string>('serverPath', '');
    if (!serverPath) {
        // Look for ETBBS.Lsp.exe in common locations
        const possiblePaths = [
            path.join(context.extensionPath, 'server', 'ETBBS.Lsp.exe'),
            path.join(context.extensionPath, 'server', 'ETBBS.Lsp'),
        ];

        // Check which server file exists
        for (const p of possiblePaths) {
            if (fs.existsSync(p)) {
                serverPath = p;
                console.log(`Found LSP server at: ${serverPath}`);
                break;
            }
        }

        // If no executable found, try dotnet with dll
        if (!serverPath) {
            const dllPath = path.join(context.extensionPath, 'server', 'ETBBS.Lsp.dll');
            if (fs.existsSync(dllPath)) {
                serverPath = dllPath;
                console.log(`Found LSP server DLL at: ${serverPath}`);
            }
        }

        // If still no server found, show error
        if (!serverPath) {
            vscode.window.showErrorMessage(
                'LBR Language Server not found. Please run prepare-server script.'
            );
            console.error('LSP server not found in:', possiblePaths);
            return;
        }
    }

    // Server options
    const serverOptions: ServerOptions = {
        command: serverPath.endsWith('.dll') ? 'dotnet' : serverPath,
        args: serverPath.endsWith('.dll') ? [serverPath] : [],
        transport: TransportKind.stdio,
        options: {
            env: {
                ...process.env,
                ETBBS_LSP_LANG: config.get<string>('locale', 'en')
            }
        }
    };

    // Client options
    const clientOptions: LanguageClientOptions = {
        documentSelector: [
            { scheme: 'file', language: 'lbr' }
        ],
        synchronize: {
            fileEvents: vscode.workspace.createFileSystemWatcher('**/*.lbr')
        },
        initializationOptions: {
            lang: config.get<string>('locale', 'en')
        }
    };

    // Create language client
    client = new LanguageClient(
        'lbrLanguageServer',
        'LBR Language Server',
        serverOptions,
        clientOptions
    );

    // Start the client and server
    client.start().then(() => {
        console.log('LBR Language Server is ready');
    }).catch((error) => {
        console.error('Failed to start LBR Language Server:', error);
    });
}

export function deactivate(): Thenable<void> | undefined {
    if (!client) {
        return undefined;
    }
    return client.stop();
}
