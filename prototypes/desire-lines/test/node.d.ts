// Minimal declarations for the Node built-ins the headless tests use
// (the project does not depend on @types/node).
declare const process: { exitCode?: number; argv: string[]; env: Record<string, string | undefined> };
declare module "node:worker_threads" {
  export const isMainThread: boolean;
  export const workerData: unknown;
  export const parentPort: { postMessage(v: unknown): void; on(ev: "message", cb: (v: never) => void): void } | null;
  export class Worker {
    constructor(url: URL, opts?: { workerData?: unknown });
    postMessage(v: unknown): void;
    on(ev: "message", cb: (v: never) => void): this;
    on(ev: "error", cb: (e: Error) => void): this;
    terminate(): Promise<number>;
  }
}
declare module "node:os" {
  export function availableParallelism(): number;
}
