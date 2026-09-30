import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import Pagination from './Pagination';

describe('Pagination Component', () => {
  const defaultProps = {
    currentPage: 2,
    totalPages: 5,
    pageSize: 50,
    totalItems: 250,
    onPageChange: jest.fn(),
    onPageSizeChange: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders page info and navigation buttons', () => {
    render(<Pagination {...defaultProps} />);

    expect(screen.getByText('Página 2 de 5 (250 total)')).toBeInTheDocument();
    expect(screen.getByText('Página 2 de 5')).toBeInTheDocument();
    expect(screen.getAllByText('← Anterior').length).toBe(2);
    expect(screen.getAllByText('Próxima →').length).toBe(2);
  });

  it('calls onPageChange when clicking previous and next', () => {
    render(<Pagination {...defaultProps} />);

    const prevButtons = screen.getAllByText('← Anterior');
    fireEvent.click(prevButtons[0]);
    expect(defaultProps.onPageChange).toHaveBeenCalledWith(1);

    const nextButtons = screen.getAllByText('Próxima →');
    fireEvent.click(nextButtons[0]);
    expect(defaultProps.onPageChange).toHaveBeenCalledWith(3);
  });

  it('disables previous button on page 1', () => {
    render(<Pagination {...defaultProps} currentPage={1} />);

    const prevButtons = screen.getAllByText('← Anterior');
    expect(prevButtons[0]).toBeDisabled();
  });

  it('disables next button on last page', () => {
    render(<Pagination {...defaultProps} currentPage={5} totalPages={5} />);

    const nextButtons = screen.getAllByText('Próxima →');
    expect(nextButtons[0]).toBeDisabled();
  });

  it('calls onPageSizeChange when clicking a page size button', () => {
    render(<Pagination {...defaultProps} />);

    const size100Btn = screen.getByText('100');
    fireEvent.click(size100Btn);
    expect(defaultProps.onPageSizeChange).toHaveBeenCalledWith(100);
  });
});
