# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  team_id  :integer
#  name     :string
#  position :integer
#  key      :string
#
# Indexes
#
#  index_players_on_key      (key) UNIQUE
#  index_players_on_team_id  (team_id)
#

module API
  module Entities
    class Player < Grape::Entity

      expose :id, :name, :position, :key

      expose :team, using: API::Entities::Team, if: :team

    end
  end
end
