import React from 'react';
import { Select } from '@mantine/core';

export interface DropdownItem {
  value: string;
  label: string;
}

export interface SearchableDropdownProps {
  id?: string;
  label?: string;
  placeholder?: string;
  data: DropdownItem[];
  value: string | null;
  onChange: (value: string | null) => void;
  className?: string;
}

const SearchableDropdown: React.FC<SearchableDropdownProps> = ({
  id,
  label,
  placeholder,
  data,
  value,
  onChange,
  className,
}) => {
  return (
    <Select
      id={id}
      label={label}
      placeholder={placeholder}
      data={data}
      value={value}
      onChange={onChange}
      searchable
      clearable
      className={className}
      nothingFoundMessage="Nenhum resultado encontrado"
    />
  );
};

export default SearchableDropdown;
