import React from 'react';
import { Title } from '@mantine/core';
import Menu from './Menu';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { UserProfile } from './UserProfile';

const MyProfilePage: React.FC = () => {
  const { currentUser } = useCurrentUser();

  if (!currentUser) {
    return (
      <Menu>
        <div className="my-profile-page" style={{ display: 'flex', justifyContent: 'center', padding: '60px' }}>
          <p>Carregando perfil...</p>
        </div>
      </Menu>
    );
  }

  return (
    <Menu>
      <div className="my-profile-page" style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
        <div className="my-profile-header" style={{ marginBottom: '20px' }}>
          <Title order={2} size="h2" className="page-title-break">Meu Perfil</Title>
        </div>
        <UserProfile userId={currentUser.id} mode="page" />
      </div>
    </Menu>
  );
};

export default MyProfilePage;
