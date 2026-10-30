export interface paths {
    "/bff/api/v1/applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** List applications through the current browser session */
        get: operations["ListBrowserApplications"];
        put?: never;
        /** Create a managed application and return its generated secret once */
        post: operations["CreateBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/antiforgery": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Acquire a native token for this browser generation's application mutations */
        get: operations["GetApplicationAntiforgeryToken"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read sanitized application details */
        get: operations["GetBrowserApplication"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/session": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** Read local browser session status without renewal */
        get: operations["ReadBrowserSession"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        ApplicationAntiforgeryToken: {
            requestToken: string;
        };
        ApplicationCreate: {
            clientId: string;
            credentialExpiresAt: string;
            displayName: string;
            redirectUris: string[];
        };
        ApplicationCredentialMetadata: {
            /** Format: date-time */
            expiresAt: string;
            /** Format: date-time */
            issuedAt: string;
        };
        ApplicationCredentialOutput: {
            application: components["schemas"]["ApplicationDetails"];
            clientSecret: string;
        };
        ApplicationDetails: {
            clientId: string;
            credential: null | components["schemas"]["ApplicationCredentialMetadata"];
            displayName: null | string;
            editable: boolean;
            /** Format: date-time */
            expiresAt: string;
            id: string;
            profile: null | string;
            redirectUris: string[];
        };
        ApplicationPage: {
            /** Format: date-time */
            expiresAt: string;
            hasNextPage: boolean;
            items: components["schemas"]["ApplicationSummary"][];
            /** Format: int32 */
            page: number | string;
            /** Format: int32 */
            pageSize: number | string;
        };
        ApplicationSummary: {
            clientId: string;
            clientType: null | string;
            displayName: null | string;
            id: string;
        };
        BrowserSessionStatus: {
            authenticated: boolean;
            /** Format: date-time */
            expiresAt?: null | string;
            subject?: null | string;
        };
        ProblemDetails: {
            detail?: null | string;
            instance?: null | string;
            /** Format: int32 */
            status?: null | number | string;
            title?: null | string;
            type?: null | string;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    ListBrowserApplications: {
        parameters: {
            query?: {
                /** @description Literal case-sensitive client-ID prefix. */
                search?: string;
                page?: number;
                pageSize?: number;
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationPage"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    CreateBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationCreate"];
            };
        };
        responses: {
            /** @description Created */
            201: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationCredentialOutput"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Conflict */
            409: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetApplicationAntiforgeryToken: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationAntiforgeryToken"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Native request tokens must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    GetBrowserApplication: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag?: string;
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["ApplicationDetails"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Unauthorized */
            401: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Forbidden */
            403: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Not Found */
            404: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
        };
    };
    ReadBrowserSession: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["BrowserSessionStatus"];
                };
            };
            /** @description Bad Request */
            400: {
                headers: {
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description Service Unavailable */
            503: {
                headers: {
                    /** @description Local authentication responses must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content?: never;
            };
        };
    };
}
