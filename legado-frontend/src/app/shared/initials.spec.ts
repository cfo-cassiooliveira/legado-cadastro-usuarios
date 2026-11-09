import { initialsOf } from './initials';

describe('initialsOf', () => {
  it.each([
    ['Ana Paula Souza', 'AS'],
    ['  maria  ', 'M'],
    ['', ''],
    ['João da Silva', 'JS'],
  ])('initialsOf("%s") = "%s"', (name, expected) => {
    expect(initialsOf(name)).toBe(expected);
  });
});
