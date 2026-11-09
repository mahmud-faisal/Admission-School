import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchJson } from './client';
import type { ClassSeats, CreateClassInput, Hold, HoldConfirmed, HoldCreated, MyHold } from './types';

/** Polling intervals (ms). Mutable so tests can shorten them. */
export const POLL = { classesMs: 3000, holdMs: 5000, myHoldsMs: 5000 };

export const queryKeys = {
  classes: ['classes'] as const,
  hold: (holdId: string) => ['holds', holdId] as const,
  myHolds: (studentId: string) => ['myHolds', studentId] as const,
};

export function useClasses() {
  return useQuery({
    queryKey: queryKeys.classes,
    queryFn: () => fetchJson<ClassSeats[]>('/classes'),
    refetchInterval: POLL.classesMs,
    placeholderData: keepPreviousData,
  });
}

export function useHold(holdId: string) {
  return useQuery({
    queryKey: queryKeys.hold(holdId),
    queryFn: () => fetchJson<Hold>(`/holds/${encodeURIComponent(holdId)}`),
    // Keep polling while the hold can still change; stop once it is final.
    refetchInterval: (query) => (query.state.data?.status === 'HELD' ? POLL.holdMs : false),
    placeholderData: keepPreviousData,
  });
}

export function useMyHolds(studentId: string) {
  return useQuery({
    queryKey: queryKeys.myHolds(studentId),
    queryFn: () => fetchJson<MyHold[]>('/me/holds', { studentId }),
    enabled: studentId !== '',
    refetchInterval: POLL.myHoldsMs,
    placeholderData: keepPreviousData,
  });
}

export function useCreateClass() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateClassInput) => fetchJson<ClassSeats>('/classes', { method: 'POST', body: input }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.classes }),
  });
}

export function useApply() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ classId, studentId }: { classId: string; studentId: string }) =>
      fetchJson<HoldCreated>(`/classes/${encodeURIComponent(classId)}/holds`, { method: 'POST', studentId }),
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
      void queryClient.invalidateQueries({ queryKey: ['myHolds'] });
    },
  });
}

function useHoldMutation<T>(holdId: string, run: (studentId: string) => Promise<T>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: run,
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.hold(holdId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
      void queryClient.invalidateQueries({ queryKey: ['myHolds'] });
    },
  });
}

export function useConfirm(holdId: string) {
  return useHoldMutation(holdId, (studentId) =>
    fetchJson<HoldConfirmed>(`/holds/${encodeURIComponent(holdId)}/confirm`, { method: 'POST', studentId }),
  );
}

export function useCancel(holdId: string) {
  return useHoldMutation(holdId, (studentId) =>
    fetchJson<void>(`/holds/${encodeURIComponent(holdId)}`, { method: 'DELETE', studentId }),
  );
}
