import { create } from 'zustand';
import { personsApi } from '@/api/persons';

interface PersonState {
  activePerson: string | null;
  personList: string[];
  isLoading: boolean;

  setActivePerson: (name: string) => void;
  fetchPersonList: () => Promise<void>;
}

export const usePersonStore = create<PersonState>((set) => ({
  activePerson: null,
  personList: [],
  isLoading: false,

  setActivePerson: (name) => set({ activePerson: name }),

  fetchPersonList: async () => {
    set({ isLoading: true });
    try {
      const res = await personsApi.list();
      const persons = res.persons;
      set({ personList: persons, isLoading: false });
      // Auto-select first person if none selected
      set((state) => ({
        activePerson: state.activePerson ?? (persons.length > 0 ? persons[0] : null),
      }));
    } catch {
      set({ isLoading: false });
    }
  },
}));
