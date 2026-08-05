/// <reference types="vite/client" />

interface Window {
	__STAYFLOW_RUNTIME_CONFIG__?: {
		apiUrl?: string;
		signalRUrl?: string;
		environment?: string;
	};
}
