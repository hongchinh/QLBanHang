import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { getApiError } from '@/lib/api-client';

// Puts the server's validation details (camelCase keys) under the matching inputs.
export function applyApiFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
) {
  const details = getApiError(error)?.details ?? {};
  for (const field of fields) {
    const messages = details[field];
    if (messages?.length) setError(field, { type: 'server', message: messages[0] });
  }
}
