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
        /** Edit application presentation and callbacks using its current strong version */
        put: operations["EditBrowserApplication"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/disable": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Disable new sign-ins and issuance without revoking existing grants */
        post: operations["DisableBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/enable": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Re-enable fresh sign-ins without restoring old attempts or extending credentials */
        post: operations["EnableBrowserApplication"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/bff/api/v1/applications/{id}/secret-replacements": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        /** Replace a managed secret once with current credential permission and fresh MFA */
        post: operations["ReplaceBrowserApplicationSecret"];
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
            enabled: null | boolean;
            /** Format: date-time */
            expiresAt: string;
            id: string;
            profile: null | string;
            redirectUris: string[];
        };
        ApplicationEdit: {
            displayName: string;
            redirectUris: string[];
        };
        ApplicationLifecycle: {
            /** @enum {boolean} */
            confirm: true;
            /** @enum {string} */
            reason: "maintenance" | "security" | "configuration";
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
        ApplicationSecretReplacement: {
            confirmed: boolean;
            credentialExpiresAt: string;
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
    EditBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationEdit"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
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
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
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
    DisableBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationLifecycle"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
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
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
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
    EnableBrowserApplication: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationLifecycle"];
            };
        };
        responses: {
            /** @description OK */
            200: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    /** @description Exact strong aggregate version; absent for protected or unmanaged registrations. */
                    ETag: string;
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
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
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
    ReplaceBrowserApplicationSecret: {
        parameters: {
            query?: never;
            header: {
                /** @description One canonical strong resource-bound ETag from the latest read or mutation; missing is 428, malformed or stale is 412. */
                "If-Match": string;
                /** @description Native token bound to this browser generation and application mutation purpose. */
                "X-CSRF-TOKEN": string;
            };
            path: {
                /** @description Exact native application identity; the BFF reserves antiforgery for its token endpoint. */
                id: string;
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["ApplicationSecretReplacement"];
            };
        };
        responses: {
            /** @description OK */
            200: {
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
            /** @description Precondition Failed */
            412: {
                headers: {
                    /** @description Application data and generated credentials must not be cached. */
                    "Cache-Control"?: "no-store";
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["ProblemDetails"];
                };
            };
            /** @description Precondition Required */
            428: {
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
