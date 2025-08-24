export function receiveMessage(
    onMessage: (topic: string, message: string) => void
): (event: MessageEvent) => void {
    return (event) => {
        var parsed = JSON.parse(event.data);
        onMessage(parsed.Topic, parsed.Message);
    }
}