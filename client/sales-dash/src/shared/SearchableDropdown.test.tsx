import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';
import SearchableDropdown from './SearchableDropdown';

// Wrap component with MantineProvider for testing
const renderWithMantine = (ui: React.ReactNode) => {
  return render(<MantineProvider>{ui}</MantineProvider>);
};

describe('SearchableDropdown', () => {
  const mockData = [
    { value: 'email1@example.com', label: 'email1@example.com' },
    { value: 'email2@example.com', label: 'email2@example.com' },
  ];

  it('renders correctly with given placeholder', () => {
    renderWithMantine(
      <SearchableDropdown
        data={mockData}
        value={null}
        onChange={() => {}}
        placeholder="Select an email"
      />
    );

    expect(screen.getByPlaceholderText('Select an email')).toBeInTheDocument();
  });

  it('renders the correct initial value', () => {
    renderWithMantine(
      <SearchableDropdown
        data={mockData}
        value="email2@example.com"
        onChange={() => {}}
        placeholder="Select an email"
      />
    );

    expect(screen.getByDisplayValue('email2@example.com')).toBeInTheDocument();
  });
});
