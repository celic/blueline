# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  team_id  :integer
#  name     :string
#  position :integer
#
# Indexes
#
#  index_players_on_team_id  (team_id)
#

class Player < ActiveRecord::Base

	## Relationships
	belongs_to :team
end
