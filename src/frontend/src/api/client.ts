import { refreshAccessToken } from "./auth";

const API_URL = "http://localhost:5000";

export async function apiRequest(
    path: string,
    options?: RequestInit,
    accessToken?: string,
    onTokenRefresh?: (token: string) => void,
    onAuthFailure?: () => void,
    retry = true
) {
    const response = await fetch(API_URL + path, {
        ...options,
        headers: {
            ...options?.headers,
            ...(accessToken ? { Authorization: "Bearer " + accessToken } : {})
        },
        credentials: "include"
    });

    if (response.status === 401 && retry) {
        const newAccessToken = await refreshAccessToken();

        if (newAccessToken) {
            onTokenRefresh?.(newAccessToken);

            return apiRequest(
                path,
                options,
                newAccessToken,
                onTokenRefresh,
                onAuthFailure,
                false
            );
        }
    }

    return response;
}