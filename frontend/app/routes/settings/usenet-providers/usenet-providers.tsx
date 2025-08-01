import { Button, Form, Modal, Table, Badge } from "react-bootstrap";
import styles from "./usenet-providers.module.css"
import { useCallback, useEffect, useState, type Dispatch, type SetStateAction } from "react";

type UsenetProvidersProps = {
    config: Record<string, string>
    setNewConfig: Dispatch<SetStateAction<Record<string, string>>>
    onReadyToSave: (isReadyToSave: boolean) => void
};

type Provider = {
    index: number;
    name: string;
    host: string;
    port: string;
    useSsl: boolean;
    user: string;
    pass: string;
    connections: string;
    priority: string;
    enabled: boolean;
    isHealthy?: boolean;
};

export function UsenetProviders({ config, setNewConfig, onReadyToSave }: UsenetProvidersProps) {
    const [providers, setProviders] = useState<Provider[]>([]);
    const [showModal, setShowModal] = useState(false);
    const [editingProvider, setEditingProvider] = useState<Provider | null>(null);
    const [testingProvider, setTestingProvider] = useState<number | null>(null);

    // Load providers from config
    useEffect(() => {
        console.log("Loading providers from config:", config);
        const providerCount = parseInt(config["usenet.providers.count"] || "0");
        console.log("Provider count:", providerCount);
        const loadedProviders: Provider[] = [];
        
        for (let i = 0; i < providerCount; i++) {
            loadedProviders.push({
                index: i,
                name: config[`usenet.provider.${i}.name`] || `Provider ${i + 1}`,
                host: config[`usenet.provider.${i}.host`] || "",
                port: config[`usenet.provider.${i}.port`] || "563",
                useSsl: config[`usenet.provider.${i}.use-ssl`] === "true",
                user: config[`usenet.provider.${i}.user`] || "",
                pass: config[`usenet.provider.${i}.pass`] || "",
                connections: config[`usenet.provider.${i}.connections`] || "10",
                priority: config[`usenet.provider.${i}.priority`] || i.toString(),
                enabled: config[`usenet.provider.${i}.enabled`] !== "false"
            });
        }
        
        setProviders(loadedProviders);
    }, [config]);

    // Update config when providers change
    const updateConfig = useCallback((updatedProviders: Provider[]) => {
        const newConfig = { ...config };
        
        // Clear old provider configs
        Object.keys(newConfig).forEach(key => {
            if (key.startsWith("usenet.provider.")) {
                delete newConfig[key];
            }
        });
        
        // Set new provider count
        newConfig["usenet.providers.count"] = updatedProviders.length.toString();
        
        // Set provider configs
        updatedProviders.forEach(provider => {
            newConfig[`usenet.provider.${provider.index}.name`] = provider.name;
            newConfig[`usenet.provider.${provider.index}.host`] = provider.host;
            newConfig[`usenet.provider.${provider.index}.port`] = provider.port;
            newConfig[`usenet.provider.${provider.index}.use-ssl`] = provider.useSsl.toString();
            newConfig[`usenet.provider.${provider.index}.user`] = provider.user;
            newConfig[`usenet.provider.${provider.index}.pass`] = provider.pass;
            newConfig[`usenet.provider.${provider.index}.connections`] = provider.connections;
            newConfig[`usenet.provider.${provider.index}.priority`] = provider.priority;
            newConfig[`usenet.provider.${provider.index}.enabled`] = provider.enabled.toString();
        });
        
        setNewConfig(newConfig);
    }, [config, setNewConfig]);

    const addProvider = useCallback(() => {
        const newProvider: Provider = {
            index: providers.length,
            name: `Provider ${providers.length + 1}`,
            host: "",
            port: "563",
            useSsl: true,
            user: "",
            pass: "",
            connections: "10",
            priority: providers.length.toString(),
            enabled: true
        };
        setEditingProvider(newProvider);
        setShowModal(true);
    }, [providers]);

    const editProvider = useCallback((provider: Provider) => {
        setEditingProvider({ ...provider });
        setShowModal(true);
    }, []);

    const deleteProvider = useCallback((index: number) => {
        const updatedProviders = providers
            .filter(p => p.index !== index)
            .map((p, newIndex) => ({ ...p, index: newIndex, priority: newIndex.toString() }));
        setProviders(updatedProviders);
        updateConfig(updatedProviders);
    }, [providers, updateConfig]);

    const saveProvider = useCallback(() => {
        if (!editingProvider) return;
        
        const updatedProviders = [...providers];
        const existingIndex = updatedProviders.findIndex(p => p.index === editingProvider.index);
        
        if (existingIndex >= 0) {
            updatedProviders[existingIndex] = editingProvider;
        } else {
            updatedProviders.push(editingProvider);
        }
        
        setProviders(updatedProviders);
        updateConfig(updatedProviders);
        setShowModal(false);
        setEditingProvider(null);
    }, [editingProvider, providers, updateConfig]);

    const testConnection = useCallback(async (provider: Provider) => {
        setTestingProvider(provider.index);
        try {
            const response = await fetch("/settings/test-usenet-connection", {
                method: "POST",
                body: (() => {
                    const form = new FormData();
                    form.append("host", provider.host);
                    form.append("port", provider.port);
                    form.append("use-ssl", provider.useSsl.toString());
                    form.append("user", provider.user);
                    form.append("pass", provider.pass);
                    return form;
                })()
            });
            const isSuccessful = response.ok && ((await response.json()) === true);
            
            // Update provider health status
            const updatedProviders = providers.map(p => 
                p.index === provider.index ? { ...p, isHealthy: isSuccessful } : p
            );
            setProviders(updatedProviders);
        } catch (error) {
            console.error("Connection test failed:", error);
            const updatedProviders = providers.map(p => 
                p.index === provider.index ? { ...p, isHealthy: false } : p
            );
            setProviders(updatedProviders);
        } finally {
            setTestingProvider(null);
        }
    }, [providers]);

    const toggleProvider = useCallback((index: number) => {
        const updatedProviders = providers.map(p => 
            p.index === index ? { ...p, enabled: !p.enabled } : p
        );
        setProviders(updatedProviders);
        updateConfig(updatedProviders);
    }, [providers, updateConfig]);

    // Check if ready to save
    useEffect(() => {
        const hasValidProviders = providers.length > 0 && providers.some(p => p.enabled);
        onReadyToSave && onReadyToSave(hasValidProviders);
    }, [providers, onReadyToSave]);

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <h5>Usenet Providers</h5>
                <Button variant="primary" onClick={addProvider}>
                    Add Provider
                </Button>
            </div>

            {providers.length === 0 ? (
                <div className={styles.emptyState}>
                    <p>No providers configured. Add a provider to get started.</p>
                    <p className="text-muted">If you had a single Usenet provider configured previously, it should have been automatically migrated here.</p>
                </div>
            ) : (
                <Table striped bordered hover>
                    <thead>
                        <tr>
                            <th>Status</th>
                            <th>Name</th>
                            <th>Host</th>
                            <th>Port</th>
                            <th>SSL</th>
                            <th>Connections</th>
                            <th>Priority</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {providers.map(provider => (
                            <tr key={provider.index}>
                                <td>
                                    <Badge bg={provider.enabled ? "success" : "secondary"}>
                                        {provider.enabled ? "Enabled" : "Disabled"}
                                    </Badge>
                                    {provider.isHealthy !== undefined && (
                                        <Badge bg={provider.isHealthy ? "success" : "danger"} className="ms-1">
                                            {provider.isHealthy ? "Healthy" : "Unhealthy"}
                                        </Badge>
                                    )}
                                </td>
                                <td>{provider.name}</td>
                                <td>{provider.host}</td>
                                <td>{provider.port}</td>
                                <td>{provider.useSsl ? "Yes" : "No"}</td>
                                <td>{provider.connections}</td>
                                <td>{provider.priority}</td>
                                <td>
                                    <Button 
                                        variant="outline-primary" 
                                        size="sm" 
                                        onClick={() => editProvider(provider)}
                                        className="me-1"
                                    >
                                        Edit
                                    </Button>
                                    <Button 
                                        variant="outline-secondary" 
                                        size="sm" 
                                        onClick={() => testConnection(provider)}
                                        disabled={testingProvider === provider.index}
                                        className="me-1"
                                    >
                                        {testingProvider === provider.index ? "Testing..." : "Test"}
                                    </Button>
                                    <Button 
                                        variant={provider.enabled ? "outline-warning" : "outline-success"} 
                                        size="sm" 
                                        onClick={() => toggleProvider(provider.index)}
                                        className="me-1"
                                    >
                                        {provider.enabled ? "Disable" : "Enable"}
                                    </Button>
                                    <Button 
                                        variant="outline-danger" 
                                        size="sm" 
                                        onClick={() => deleteProvider(provider.index)}
                                    >
                                        Delete
                                    </Button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </Table>
            )}

            {/* Edit/Add Provider Modal */}
            <Modal show={showModal} onHide={() => setShowModal(false)} size="lg">
                <Modal.Header closeButton>
                    <Modal.Title>
                        {editingProvider?.index !== undefined && providers.some(p => p.index === editingProvider.index) 
                            ? "Edit Provider" : "Add Provider"}
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {editingProvider && (
                        <Form>
                            <Form.Group className="mb-3">
                                <Form.Label>Name</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.name}
                                    onChange={e => setEditingProvider({...editingProvider, name: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Group className="mb-3">
                                <Form.Label>Host</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.host}
                                    onChange={e => setEditingProvider({...editingProvider, host: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Group className="mb-3">
                                <Form.Label>Port</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.port}
                                    onChange={e => setEditingProvider({...editingProvider, port: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Check
                                type="checkbox"
                                label="Use SSL"
                                checked={editingProvider.useSsl}
                                onChange={e => setEditingProvider({...editingProvider, useSsl: e.target.checked})}
                                className="mb-3"
                            />

                            <Form.Group className="mb-3">
                                <Form.Label>Username</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.user}
                                    onChange={e => setEditingProvider({...editingProvider, user: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Group className="mb-3">
                                <Form.Label>Password</Form.Label>
                                <Form.Control
                                    type="password"
                                    value={editingProvider.pass}
                                    onChange={e => setEditingProvider({...editingProvider, pass: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Group className="mb-3">
                                <Form.Label>Max Connections</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.connections}
                                    onChange={e => setEditingProvider({...editingProvider, connections: e.target.value})}
                                />
                            </Form.Group>

                            <Form.Group className="mb-3">
                                <Form.Label>Priority</Form.Label>
                                <Form.Control
                                    type="text"
                                    value={editingProvider.priority}
                                    onChange={e => setEditingProvider({...editingProvider, priority: e.target.value})}
                                />
                                <Form.Text className="text-muted">
                                    Lower numbers have higher priority
                                </Form.Text>
                            </Form.Group>

                            <Form.Check
                                type="checkbox"
                                label="Enabled"
                                checked={editingProvider.enabled}
                                onChange={e => setEditingProvider({...editingProvider, enabled: e.target.checked})}
                            />
                        </Form>
                    )}
                </Modal.Body>
                <Modal.Footer>
                    <Button variant="secondary" onClick={() => setShowModal(false)}>
                        Cancel
                    </Button>
                    <Button variant="primary" onClick={saveProvider}>
                        Save Provider
                    </Button>
                </Modal.Footer>
            </Modal>
        </div>
    );
}